# SPEC-17 — WhatsApp command agent and AI provider settings

## Problem

Temporary notices (SPEC-16) exist only in the web UI. The people who need them are on
the phone: a contact wants to text the paired WhatsApp number "Você poderia ativar os
alertas?" / "Desativar alertas" and have its own temporary notice start or end, with a
confirmation back. The phrasing is free, so an optional AI model must catch what
keyword rules miss — and the model, provider and key are a system setting.

Scope: enable / disable the sender's own WhatsApp notices, plus the "até quando?"
follow-up. The intent set is an enum so more commands can be added later — SPEC-18
adds the read-only "status" and "últimas N capturas" reports.

## Flow

```
phone → Evolution MESSAGES_UPSERT webhook
      → POST /api/whatsapp/webhook (API, X-Webhook-Key) → WhatsAppCommandLogs row (Pending, unique MessageId)
      → WhatsAppCommandHostedService (web app, 2 s)
            contact lookup by number → IIntentClassifier (rules → configured LLM)
            → TemporaryNoticeService (activate / extend / end) → reply via Evolution → row Done / Ignored / Failed
```

The API only stores (fast, key-guarded, reachable from the Docker network as
`host.docker.internal:5220`); the web app is the only process that sends and the only
one that holds the AI key — the same split as `AlertDispatcher` → `AlertDeliveryHostedService`.

## Inbound webhook

- Registered per instance from `/system/whatsapp` (**Registrar webhook** →
  `POST webhook/set/{instance}` with `events: [MESSAGES_UPSERT]` and the header
  `X-Webhook-Key: <secret>`), never through `WEBHOOK_GLOBAL_*` env vars (global, no
  custom header, needs a container restart). `GET webhook/find/{instance}` feeds the
  status chip. The URL and the secret live on `SystemSettings` (`WhatsAppWebhookUrl`,
  `WhatsAppWebhookSecret`), so the API and the web app agree; the secret is generated
  on the page (24 random bytes, url-safe base64).
- `POST /api/whatsapp/webhook`: 401 when the secret is unset or the header mismatches
  (the instance `apikey` echoed in the payload is accepted as a fallback when it matches
  the configured Evolution key); otherwise **always 200** — Evolution retries non-2xx
  and a retry must never run a command twice. Response `{ stored, reason }`.
- `WhatsAppInboundMessage.TryParse` (Core, pure) keeps only `messages.upsert`, not
  `fromMe`, person JIDs (`@s.whatsapp.net`; `@lid` only with `senderPn`/`remoteJidAlt`),
  with text (`conversation` / `extendedTextMessage.text`, ephemeral variants). Groups,
  broadcasts, media, reactions and other events are dropped before storage.
- `WhatsAppCommandLog`: MessageId (unique index — `TryAddAsync` swallows the race),
  sender JID/number/pushName, text (≤ 1000), MessageAt/ReceivedAt, Status
  (Pending / AwaitingDuration / Done / Ignored / Failed), Detail, TenantId, ContactId
  (SetNull), Intent, IntentSource (`rules` / `llm` / `error`), TriggersAffected,
  ReplyText, ProcessedAt. It is the audit log, the dedupe key and the conversation state.

## Processing (`WhatsAppCommandHostedService`, web app, every 2 s)

Per pending row, in order: agent switched off → Ignored; message older than 10 min
(replayed after a reconnect) → Ignored; sender number not a contact → Ignored
**silently** (strangers get no reply); more than 5 messages in the last minute →
Ignored. The number is matched through `WhatsAppNumberMatcher.Candidates` (JID digits
plus the Brazilian ninth-digit variant, because JIDs often omit it). Then:

- **Enable with a validity** ("ativar alertas por 2 horas", "até as 22h", "até eu
  desativar") → `ActivateForContactAsync` → confirmation with the end.
- **Enable without a validity** → activated for `WhatsAppBotDefaultHours` (8) and the
  reply asks *"Até quando? (ex.: 2 horas, até as 22h, até amanhã, até eu desativar)"*;
  the row becomes `AwaitingDuration`. The sender's next message within 10 min is
  classified with `expectingDuration`: a bare validity → `SetExpiryForContactAsync`
  (or a fresh activation when the notice already ended) → "Combinado: … até …".
- **Disable** → `EndForContactAsync` → "Alertas temporários encerrados em N regra(s)."
  or "Não havia alertas temporários ativos…".
- **Unknown** → help text; while a validity is awaited the state is kept.

A contact registered in several tenants gets the command applied to each. When the
tenant's WhatsApp master switch is off the reply carries a note. The reply is sent
with `IEvolutionApiClient.SendTextAsync`; a send failure marks the row Failed (the
command itself already ran).

## Interpretation (`IIntentClassifier`)

- Tier 1 — `CommandTextRules` (Core, pure): fold accents/punctuation, then verb families
  (enable: `ativ|lig|habilit|inici|comec|quero receber`; disable: `desativ|deslig|
  desabilit|parar|pare|cancel|encerr|suspend|silenci|nao quero`) and nouns
  (`alert|avis|notific`). A match needs exactly one family and (a noun or ≤ 4 words);
  a "não" next to an enable verb or both families → undecided. `CommandDurationRules`
  parses "N horas", "meia hora", "N minutos", "até as HH(:mm)", "até HHh(mm)", "até
  amanhã (às H)", "hoje / fim do dia / meia-noite", "N dias", "até eu desativar / sem
  prazo". Clock times already past mean tomorrow; "amanhã" alone is 08:00.
- Tier 2 — `LlmIntentClassifier` (Infrastructure): only when the rules are undecided
  and `SystemSettings.AiProvider` ≠ None with a model and key. One short call
  (≤ 500 chars of text, 15 s timeout) asking for
  `{"intent": enable|disable|set_duration|unknown, "until": ISO|null, "until_disabled": bool}`;
  the answer is parsed leniently (code fences stripped, outermost object) and only ever
  yields an enum value plus a validity. Any failure → Unknown with source `error`.
- Providers (`ILlmClient`, never throw): Claude through the official `Anthropic` NuGet
  (`OutputConfig.Effort = Low` except on Haiku), Gemini through REST `generateContent`
  (JSON response mode, thought parts skipped), DeepSeek through its OpenAI-compatible
  `chat/completions` (JSON mode).

## Temporary notices, shared (`TemporaryNoticeService`, Core)

`ActivateAsync` (dialog), `ActivateForContactAsync` (agent: one notice per enabled
rule; the sender's own running notice on a rule is extended instead of duplicated),
`SetExpiryForContactAsync`, `EndForContactAsync` (sole-contact triggers are deleted,
shared ones just lose the sender), `EndAllRunningAsync` (rules page banner). Validity
is clamped to 1–72 h; null = until ended. `AlertTrigger.IsRunningTemporaryAt` is the
single definition of "running".

## Settings and UI

- `SystemSettings`: `AiProvider` (None / Gemini / Claude / DeepSeek), `AiModel`,
  `AiApiKey`, `WhatsAppBotEnabled`, `WhatsAppBotDefaultHours`, `WhatsAppWebhookUrl`,
  `WhatsAppWebhookSecret`. Migration `WhatsAppCommandAgent`.
- `/system/ai` (SuperAdmin, nav *Sistema → Inteligência artificial*): provider,
  model (name with a small silver caption: custo baixo / médio / alto — see
  `AiModelCatalog`), API key, **Teste de interpretação** (runs the classifier with the
  saved settings). Catalog: Gemini 3.5 Flash-Lite / 3.7 Flash / 3.1 Pro, Claude Haiku
  4.5 / Sonnet 5 / Opus 5, DeepSeek V4 Flash / V4 Pro.
- `/system/whatsapp`, section **Agente de comandos**: switch, default hours, webhook
  URL, secret + *Gerar segredo*, *Registrar webhook* with the status chip, and the last
  20 received messages (sender, text, intent + source, status, detail).

Tests (`tests/CameraVision.Core.Tests`): `CommandTextRulesTests`,
`CommandDurationRulesTests`, `WhatsAppInboundMessageTests`, `WhatsAppNumberMatcherTests`,
`TemporaryNoticeServiceTests` (hand-written fake repository), `AlertTriggerTests`.
