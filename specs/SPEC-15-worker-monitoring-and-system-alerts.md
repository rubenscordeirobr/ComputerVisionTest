# SPEC-15 — Worker monitoring, system alerts and settings pages

## Problem

The Cameras page said "Online" as long as the camera answered ping/TCP, even when
`CameraVision.DetectionWorker` was not running — so nothing was actually being
detected or recorded and nobody noticed. There was also no notification when the
worker died, and every system configuration lived on one long page.

## Worker liveness

- The worker now POSTs a **global heartbeat** every `api.statusIntervalSeconds`
  (30 s) to `POST /api/processor/heartbeat` (X-Api-Key): started-at, inference
  device, active camera count. Stored in the singleton `WorkerStatus` row (Id 1).
- Per-camera `Camera.ProcessorStatusAt` (existing) keeps being written by the
  per-camera status reports.
- `Core.Health.WorkerHealth`: a report older than **35 s** (heartbeat + grace) is
  *stale* — the worker stopped updating. Stale is deliberately "neither online
  nor offline".
- `CameraVision.Web` runs `WorkerHealthMonitor` (BackgroundService, every 10 s,
  `WorkerHealth:IntervalSeconds`): last-seen = max(global heartbeat, newest
  camera status) — the fallback keeps older workers working. Exposes
  `IWorkerHealthService` (snapshot + Changed event) to the UI.

## UI states

- Cameras page **Status** column: `Offline` (probe failed) wins; probe OK but
  worker stale/never-processed → **"Sem processamento"** (warning chip +
  explanatory tooltip); probe OK + worker fresh → `Online`.
- **Processador** column: stale reports show **"Sem resposta"** (error) instead
  of silently becoming "—"; tooltip shows the last update time. The page reloads
  cameras on every probe cycle so the timestamps stay fresh.
- Cameras page + dashboard show a red banner while the worker is stale; the
  dashboard gains a "Processador de vídeo" card (Em execução / Parado / Nunca
  conectado + last update).

## Critical system alerts (admin)

- `AdminAlertSettings` singleton (SuperAdmin scope, seeded enabled): master
  switch, per-channel switches, **admin e-mails** and **admin WhatsApp numbers**
  (separate from tenant recipients), `WorkerDownAfterSeconds` (default 90,
  floor 45), `CooldownMinutes` (default 30), `NotifyRecovery`.
- `WorkerHealthMonitor` state machine: transition to *down* after
  `WorkerDownAfterSeconds` without updates (never-seen counts only while
  processable cameras exist), *recovered* on the next update. On web startup the
  verdict is reconciled against the last persisted event so outages that
  happened while the web app was off still notify — and web restarts during one
  outage do not re-notify.
- Transitions are recorded in `SystemAlertEvents` (always), and notified through
  the existing `IAlertChannel`s via `AdminAlertNotifier` (transient
  `AlertSettings` carrying the admin recipients). Cooldown applies per event
  type; suppressed/failed sends keep `NotifiedAt` null.
- Worker in fallback mode (API down) cannot report — that correctly counts as
  "not updating" from the web's point of view.

## Settings refactor

`/system-settings` was split into one page per concern, grouped in a "Sistema"
nav group (SuperAdmin):

| Route                 | Page                                                     |
| --------------------- | -------------------------------------------------------- |
| `/system/smtp`        | E-mail (SMTP) — also answers legacy `/system-settings`   |
| `/system/application` | Aplicação (URL pública)                                  |
| `/system/whatsapp`    | WhatsApp (Evolution API) + pareamento QR                 |
| `/system/alerts`      | Alertas do sistema: worker status, admin recipients, test send, event history |

The "Enviar alerta de teste" button sends through the current (possibly
unsaved) form so delivery can be validated before saving.
