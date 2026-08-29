# SPEC-13 — Camera health alerts: offline/weak, anti-flood, digest, history

## Objective

Alert (e-mail live, WhatsApp stub) when a camera turns **Offline** or **Weak**,
with debounce against false positives, optional **recovery** notifications,
per-camera+condition **cooldown**, a global **flood cap**, an optional
**digest mode**, and a persisted **health event history** with a simple UI —
so the user is informed but never spammed, and suppressed events are never
lost.

## Scope

- `HealthAlertSettings` singleton + `CameraHealthEvent` history table
  (+ repositories, migration).
- Health state machine layered on the existing `CameraHealthMonitor` probes.
- Notification pipeline with documented precedence (below).
- Digest background job (interval-based, PT-BR summary message).
- Settings UI (new **Saúde das câmeras** tab on `/alerts`) and history UI
  (**Histórico** action per camera on `/cameras`).

## Out of scope

- WhatsApp sending (stub logs, as everywhere).
- Alerting on DetectionWorker status (informational column only, v1).

## Dependencies

- SPEC-03 (probe monitor), SPEC-10 (`AlertMessage`/channel abstraction),
  SPEC-06 (recipients/master switches).

## Conditions & debounce

Each probe cycle classifies an enabled camera:

- **Offline** — TCP probe failed;
- **Weak** — online but degraded: latency > `WeakLatencyMs` (default 500), or
  intermittent failures (≥ `ConsecutiveChecks` failed probes within the last
  `2 × ConsecutiveChecks` cycles without ever hitting the consecutive
  threshold);
- **Healthy** — online, latency within the threshold.

A camera's *alert state* only transitions after the new condition holds for
`ConsecutiveChecks` consecutive cycles (default 3). Transitions **to**
Offline/Weak record an event of that condition; transitions **back to**
Healthy record a `Recovered` event (notified only when `NotifyRecovery` is on).

## Notification precedence (cooldown → flood cap → digest)

For every recorded event, in order:

1. **Cooldown** — if a notification for the *same camera + condition* was sent
   in the last `CooldownMinutes` (default 10): suppress (event kept,
   `Suppressed = true`).
2. **Flood cap (global)** — if ≥ `FloodCapCount` (default 10) notifications
   were sent in the last `FloodCapWindowMinutes` (default 60): suppress/hold.
3. **Digest mode** — when `DigestEnabled`: *no individual health messages at
   all*; every event (fresh or suppressed) waits for the digest job, which
   every `DigestIntervalMinutes` (default 15) sends one grouped PT-BR message
   (`Resumo: 3 eventos — Garagem offline 14:02; Portão sinal fraco 14:05;
   Garagem normalizada 14:10.`) covering all events not yet notified/digested,
   then stamps them `DigestedAt`. When digest is **off**, events that pass
   steps 1–2 are sent individually and immediately.

Suppressed/held events are never lost: they stay in history and are included
in the next digest whenever digest mode is (or becomes) enabled.

## Domain model

| Entity | Fields |
|---|---|
| `HealthAlertSettings` (Id = 1) | `Enabled`, `NotifyEmail`, `NotifyWhatsApp`, `WeakLatencyMs` (500), `ConsecutiveChecks` (3), `NotifyRecovery` (true), `CooldownMinutes` (10), `FloodCapCount` (10), `FloodCapWindowMinutes` (60), `DigestEnabled` (false), `DigestIntervalMinutes` (15), `LastDigestAt?` |
| `CameraHealthEvent` | `Id`, `CameraId?` (FK set-null), `CameraName`, `Condition` (enum `Offline`/`Weak`/`Recovered`, string), `OccurredAt`, `NotifiedAt?`, `Suppressed` (bool), `DigestedAt?` — indexes on (`CameraId`,`OccurredAt`) and `OccurredAt` |

## Tasks

- [ ] `Core`: entities, `HealthCondition` enum, `ICameraHealthEventRepository`
      (`AddAsync`, `GetRecentByCameraAsync`, `CountNotifiedSinceAsync`,
      `GetLastNotifiedAtAsync(camera, condition)`,
      `GetPendingForDigestAsync`, `MarkNotified/MarkDigested`),
      `IHealthAlertSettings` accessors on `ISettingsRepository`.
- [ ] Migration `HealthAlerts` (both tables, settings row seeded).
- [ ] `Web`: `CameraHealthAlertService` — consumes each probe cycle's results
      (hooked into `CameraHealthMonitor`), keeps per-camera ring buffers +
      consecutive counters, applies the state machine + precedence, composes
      PT-BR `AlertMessage`s (e.g. subject
      `Câmera Garagem está offline` / `sinal fraco (820 ms)` /
      `normalizada`), sends via the channels enabled in the settings
      (respecting each channel's master switch/recipients).
- [ ] `Web`: `HealthDigestHostedService` — every minute checks
      `DigestEnabled` + interval elapsed + pending events → sends the digest,
      stamps `DigestedAt`/`LastDigestAt`.
- [ ] `/alerts` page: third tab **Saúde das câmeras** with the full settings
      form (PT-BR labels, numeric ranges, section «Antiflood», helper text
      explaining the precedence) + Salvar.
- [ ] `/cameras` page: **Histórico** icon per row → dialog listing recent
      events (Quando, Condição chip: Offline vermelho / Sinal fraco amarelo /
      Normalizada verde, Notificação: Enviada HH:mm / Suprimida / Em resumo /
      —).
- [ ] Recovery, cooldown, cap and digest behaviors covered by manual
      verification (unplug/simulate a camera, watch history + log).

## Acceptance criteria

- A camera unreachable for ≥ 3 consecutive checks produces exactly one
  Offline event + (channels enabled) one notification; earlier flaps produce
  nothing; recovery produces a `Recovered` event and notifies only when
  enabled.
- Latency above the threshold (or intermittent failures) yields **Weak**
  under the same debounce.
- With cooldown active, repeated same-camera/same-condition events are
  suppressed (visible in history as Suprimida) and not re-sent; the global
  cap halts individual sends across all cameras; enabling digest replaces
  individual messages with one grouped summary per interval containing every
  pending event.
- History dialog shows the audit trail even for fully suppressed periods.
- All settings persist; UI PT-BR; solution builds; capture alerts (SPEC-10)
  unaffected.

## Changelog

- 2026-08-29 — Initial version (v2 refactor request).
