# SPEC-18 — WhatsApp agent: camera status report and "últimas N capturas"

Extends SPEC-17 with two **read-only** intents. Same plumbing (webhook → command log →
`WhatsAppCommandHostedService` → reply); nothing here changes triggers, and the
"WhatsApp channel off" note is not appended.

## Problem

During the SPEC-17 test the owner asked the agent things it could not answer: "você
poderia me informar a saúde das câmeras", "status", "vc pode me enviar a lista das
últimas X capturas de pessoas". The missing alert of that test was in fact the
detection worker being down — exactly what a status question should reveal.

## Intents

- `CommandIntent.CameraStatus` — one message: the detection worker's liveness and one
  line per camera of the sender's tenant.
- `CommandIntent.ListCaptures` — one message: the latest captures (default 5, at most
  10), optionally of one object class, each with its tokenized watch link.
  `CommandInterpretation` carries `Count` (raw number asked for, clamped when used),
  `ObjectClass` (COCO name) and `UnknownClass` (the word that matched no class).
  `IsReadOnly` groups the two.

## Interpretation

- Rules (`CommandTextRules`, after the enable/disable check):
  - status cues (`status`, `saúde`/`sudade`, `situação`, `estado`, `como estão`,
    `funcionando`, `ligadas`, `online`/`offline`, `fora do ar`, `caiu`) with a noun
    (`câmera(s)`, `processador`, `sistema`, `detecção`, `worker`) or a ≤ 4-word message
    → `CameraStatus`.
  - capture nouns (`captura(s)`, `gravação`, `vídeo(s)`, `detecções`, `registros`) with a
    listing verb (`últimas`, `lista`, `enviar`, `mandar`, `mostrar`, `ver`, `quais`,
    `quero`, `recentes`) or a ≤ 3-word message → `ListCaptures`; count = first number
    (digits or `uma…dez`); the object = the phrase after `de/das/dos/com` following the
    noun, up to `hoje/ontem/agora/recentes`, resolved by `DetectableClassResolver`.
  - `DetectableClassResolver` (Core, pure): folds accents, singularizes PT-BR plurals
    (`cães → cão`, `caminhões → caminhão`, `carros → carro`), then reverse-maps the
    `DetectableClasses` labels plus synonyms (`gente/humano/pessoal → person`,
    `cão/cachorro → dog`, `veículo → car`, `moto → motorcycle`, `ônibus → bus`,
    `pássaro/ave → bird`, …); English COCO names pass through. Unknown → null and the
    word is echoed back in the reply.
  - The `liga/desliga` enable/disable verbs are now exact forms (`liga`, `ligar`,
    `ligue`…) so "câmeras ligadas?" is a status question, not an enable.
- LLM (`LlmIntentClassifier`): the prompt lists four capabilities and the JSON gains
  `"intent": … | "camera_status" | "list_captures"`, `"count"`, `"object_class"`; the
  class is validated through `DetectableClassResolver`.

## Reports (Core, pure, unit-tested)

- `CameraStatusReport.Compose(WorkerHealthSnapshot?, IReadOnlyList<CameraStatusLine>, now)`:
  ```
  Status — CameraVision (03/09 15:40)
  Processador de vídeo: em execução · CUDA (RTX 5060) · 3 câmera(s) · último sinal 15:39:58.
     | parado — sem atualização desde 03/09 01:28:27 (há 14 h 12 min). Nenhuma detecção está sendo processada.
     | nunca conectado — inicie o CameraVision.DetectionWorker.

  Câmeras:
  • Garagem — Online · 12 ms
  • Portão — Sem processamento (sem atualização desde 03/09 01:28)
  • Quintal — Offline
  ```
  Camera labels are the Câmeras page's (`Desativada`, `Sem stream`, `Offline`,
  `Verificando…`, `Sem processamento`, `Online`); the worker line follows the Painel
  card. `TimeText` moved from the web app to Core for the "há …" text.
- `CaptureListReport.Compose(items, total, requested, objectClass, link)`:
  ```
  Últimas 5 capturas de pessoa — CameraVision
  1. 03/09 14:02 · Garagem · 00:12 · https://…/captures/412/watch?token=…
  …

  Mostrando 5 de 404. Envie "últimas 10 capturas de pessoa" para ver mais.
  ```
  plus `(O máximo por mensagem é 10.)` when more was asked; without a class each line
  also names the object; empty → `Nenhuma captura de pessoa encontrada.`;
  `UnknownClass(word)` → `Não reconheci "…" como um objeto detectável. Exemplos: …`.

## Processing

`WhatsAppCommandHostedService` gains `ICameraRepository`, `ICaptureRepository`,
`ICameraHealthService`, `IWorkerHealthService`, `CaptureLinkService` and
`CaptureAlertComposer` (for `ResolveBaseUrl`). Status: cameras of each matched tenant →
`ICameraHealthService.TryGet` → report (worker liveness is global). Captures:
`ICaptureRepository.QueryAsync(new CaptureFilter { TenantId, ObjectClass, Take })` per
tenant, newest first across tenants, links via `CaptureLinkService.PlaybackUrl(id,
baseUrl)`. The row's `Detail` records "N câmera(s)" / "N captura(s)"; `TriggersAffected`
stays 0. The `Unknown` help reply now lists all four commands; the `/system/ai` test
shows count and object.

## Tests

`CommandTextRulesTests` (status and capture phrasings, raw count, unknown object),
`DetectableClassResolverTests`, `CameraStatusReportTests`, `CaptureListReportTests`.
