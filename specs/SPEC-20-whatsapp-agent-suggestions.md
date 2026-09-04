# SPEC-20 — WhatsApp agent: unsupported requests, suggestions and the offered fallback

Extends SPEC-17/18/19. When the LLM understands what a contact asked for but the agent has
no such capability, the agent says so, records the request as a **suggestion** for the
SuperAdmin and offers the closest supported command, which runs when the sender confirms.

## Problem

With an AI provider configured, a message outside the four intents still got the flat
"Não entendi. Comandos que eu atendo: …" text, because the model could only pick one of the
existing enum values. The owner asked for two things: "Entendi que você quer X, porém isso
ainda não está implementado — vamos salvar sua sugestão", stored where the SuperAdmin can
see it; and a real attempt to help — "últimas 5 capturas de pessoas de camisa amarela"
cannot filter by clothing, but listing the last 5 person captures can be offered.

## Flow

```
message → CommandTextRules (unchanged) → LlmIntentClassifier
   model answers intent "unsupported" + request (PT-BR summary) + fallback (nearest of the 4 commands, or null)
→ WhatsAppCommandHostedService
   Unsupported: AgentSuggestions row · reply "Entendi que você quer {request}. Ainda não tenho essa
                função … anotei sua sugestão … Quer que eu {offer}? Responda \"sim\"."
                with a fallback: row.Status = AwaitingConfirmation, row.FollowUpJson = fallback
   next message ≤ 10 min: "sim/ok/pode/manda…" → Confirm → the stored fallback runs (source "offer")
                          "não/deixa/depois…"  → Decline → "Tudo bem! Se precisar, é só chamar."
                          anything else → normal pipeline (an Unknown keeps the offer alive)
→ /system/suggestions (SuperAdmin): list, mark as seen, delete
```

## Interpretation

- `CommandIntent` gains `Unsupported`, `Confirm`, `Decline`. `CommandInterpretation` gains
  `Request` (the model's summary, sanitized: one line, unquoted, ≤ 200 chars — empty means
  Unknown) and `Fallback` (kept only when `IsExecutable`: enable / disable / camera_status /
  list_captures). `ToJson` / `TryFromJson` serialize an offer onto the command log.
- `ConversationState(ExpectingDuration, PendingOffer)` replaces the `expectingDuration`
  flag of `IIntentClassifier.ClassifyAsync`; the hosted service builds it from the sender's
  last answered row within the 10-minute follow-up window (`AwaitingDuration` → expecting a
  validity, `AwaitingConfirmation` → the offer in `FollowUpJson`).
- `CommandTextRules.TryMatchConfirmation` (Core, pure): ≤ 4 folded words; a message naming
  alerts, status or captures is a command in its own right (null); `nao | n | deixa |
  depois | negativo | dispens* | nem | cancel*` → false (wins over yes words: "não,
  obrigado"); `sim | s | ok | pode | claro | quero | mand* | envi* | isso | bora | beleza |
  blz | positivo | com certeza | favor | pf(v) | ta | tudo bem | aceito | yes | vai | faz`
  or 👍 / 👌 / ✅ → true; else null. `LlmIntentClassifier` checks it first while an offer
  is pending, so "não quero" declines instead of disabling the alerts.
- Tentative rule matches: `CommandTextRules` flags a `ListCaptures` whose object phrase
  carries words it could not read ("pessoas **de camisa amarela**", "carros **na garagem**",
  "pessoas **e cachorros**") or no known class at all (`CommandInterpretation.Tentative`).
  `LlmIntentClassifier` then asks the model first when one is configured, and keeps the
  rules' reading when there is no model, the call fails, or the model answers `unknown` —
  so without AI the behaviour is unchanged.
- `LlmAnswerParser` (Core, moved from `LlmIntentClassifier.Parse`, unit-tested): the JSON
  gains `"intent": … | "unsupported" | "confirm" | "decline"`, `"request"` and a nested
  `"fallback"` object parsed with the same per-intent rules but restricted to runnable
  intents (`set_duration`, `unknown`, `unsupported`, `confirm`, `decline` → no fallback).
- The prompt describes the four capabilities, what the detector cannot tell (clothing,
  colours, faces, names, ages) and what the assistant cannot do (live video, gates,
  cameras, rules, deleting, a human), asks for `request` in PT-BR infinitive ≤ 120 chars
  and the closest partial `fallback`; while an offer is pending it quotes the offer JSON
  and allows `confirm` / `decline`. Max answer tokens 400 (was 256).

## Processing (`WhatsAppCommandHostedService`)

The intent switch moved to `ExecuteAsync(interpretation, row, matches, settings, now,
state)` → `(reply, status)`, shared by the direct path and by a confirmed offer:

- `Unsupported` → `IAgentSuggestionRepository.AddAsync` (tenant and contact of the first
  match, `CommandLogId`, sender, text, request, `SystemSettings.AiModel`), Detail "Sugestão
  registrada[ · alternativa oferecida]", reply `CommandReplyText.Unsupported(request,
  offer)` where `Offer(fallback)` is "envie as últimas N capturas de pessoa" / "informe o
  status das câmeras" / "ative os alertas no seu WhatsApp" / "desative os alertas". With an
  offer the row ends `AwaitingConfirmation` with `FollowUpJson`; without one the help lines
  follow the apology.
- `Confirm` → the pending offer runs with source `offer` (log shows "Oferta aceita");
  without a pending offer it is an Unknown. `Decline` → "Tudo bem! Se precisar, é só
  chamar. 👋", Detail "Oferta recusada".
- `Unknown` while an offer is pending keeps `AwaitingConfirmation` (the offer is copied to
  the new row), like the existing `AwaitingDuration` behaviour.
- The "canal WhatsApp desativado" note is appended only to `EnableAlerts` / `SetDuration`.

## Data and UI

- `AgentSuggestion` (`AgentSuggestions`): TenantId (Restrict), ContactId (SetNull),
  CommandLogId (SetNull), SenderNumber, PushName, MessageText (≤ 1000), Request (≤ 200),
  Model, CreatedAt, ReviewedAt (null = new); index (ReviewedAt, CreatedAt).
  `WhatsAppCommandLog.FollowUpJson` (≤ 500) and `WhatsAppCommandStatus.AwaitingConfirmation`.
  Migration `AgentSuggestions`.
- `/system/suggestions` (SuperAdmin, nav *Sistema → Sugestões do agente*): count of new
  suggestions, table (quando, cliente, remetente, mensagem, pedido entendido, modelo,
  status Nova / Vista em …), **Marcar como vista**, **Excluir** (with confirmation).
- `/system/whatsapp`: intent labels "Não implementado" / "Confirmação" / "Recusa", source
  "Oferta aceita", status chip "Aguardando confirmação", link to the suggestions page.
- `/system/ai` *Teste de interpretação* shows `pedido: "…"` and `alternativa oferecida: …`.

## Tests

`LlmAnswerParserTests` (unsupported with/without request, fallback validation and class
resolution, confirm/decline, fences, past validity, garbage), `CommandInterpretationTests`
(request cleaning and cap, runnable fallbacks, JSON round trip), `CommandTextRulesTests`
(`TryMatchConfirmation` yes / no / undecided).
