# SPEC-10 — Capture rules (multiple rules replace the singleton settings)

## Objective

Replace the single `CaptureSettings` row with a user-managed **list of capture
rules** — e.g. "track cat → send e-mail", "track person → send WhatsApp" — where
each rule defines what to record and which alert channels to notify. Rework the
alert dispatcher around rules and generalize the channel abstraction so
non-capture messages (SPEC-13 health alerts) can reuse it.

## Scope

- `CaptureRule` entity + repository + migration (including data migration of
  the existing singleton into the first rule; `CaptureSettings` table dropped).
- `AlertSettings.TriggerClasses` removed — rules now decide what triggers;
  `AlertSettings` keeps the per-channel master switch + recipients.
- `/capture-settings` page becomes **Regras de Captura**: rules table + CRUD
  dialog.
- Dispatcher refactor: a new capture matches all enabled rules containing its
  class; the union of the matched rules' channels is notified (one message per
  channel, deduplicated).
- `IAlertChannel` generalized to send an `AlertMessage(Subject, HtmlBody,
  TextBody, InlineImagePath?)` — capture e-mail composition moves into the
  dispatcher; channels become content-agnostic.

## Out of scope

- Per-camera rule scoping (rules apply to all cameras in v1).
- The DetectionWorker consuming rules (SPEC-12 wires that via the API).

## Dependencies

- SPEC-01/04/06/09 (entities, pages, dispatcher being reworked).

## Domain model

| Entity | Fields | Notes |
|---|---|---|
| `CaptureRule` | `Id`, `Name` (required, max 100), `Enabled` (default true), `Classes` (`List<string>`, JSON), `ConfidenceThreshold` (0.05–0.95, default 0.5), `MaxSegmentSeconds` (5–3600, default 60), `LingerSeconds` (0–300, default 2.0), `NotifyEmail` (bool), `NotifyWhatsApp` (bool), `CreatedAt` | Replaces `CaptureSettings` |

Merged recording config for the worker (served by SPEC-11's API): classes =
union of enabled rules; per-class confidence = **min** over rules containing
the class; `maxSegmentSeconds`/`lingerSeconds` = **max** over enabled rules.

## Tasks

- [ ] `Core`: `CaptureRule` entity, `ICaptureRuleRepository` (CRUD +
      `GetEnabledAsync`); remove `CaptureSettings` entity and its
      `ISettingsRepository` methods; drop `TriggerClasses` from `AlertSettings`.
- [ ] `Core`: replace `IAlertChannel.TrySendAsync(CaptureAlert, …)` with
      `TrySendAsync(AlertMessage, AlertSettings, SystemSettings, ct)`;
      `EmailAlertChannel` renders subject/HTML/text + CID image from the
      message; WhatsApp stub unchanged in spirit.
- [ ] Migration `CaptureRules`: create `CaptureRules`; copy the old singleton
      into rule "Regra 1" via SQL (`NotifyEmail` seeded from the Email
      channel's current `Enabled`); drop `CaptureSettings` and
      `AlertSettings.TriggerClasses`.
- [ ] Dispatcher: for each fresh capture (recency guard kept) find matching
      enabled rules → union channels → compose the capture `AlertMessage`
      (same PT-BR content as SPEC-09) → send once per channel, respecting the
      channel's master `Enabled` + recipients.
- [ ] `/capture-settings` page → **Regras de Captura**: table (Nome, Classes
      as PT-BR chips, Confiança, Segmento, Espera, canal icons, Ativa inline
      switch, editar/excluir) + `CaptureRuleDialog` with all fields (classes
      multi-select, numeric ranges, channel switches); delete with
      confirmation; nav label updated.
- [ ] Alerts page: remove the trigger-classes select; add a PT-BR hint that
      classes/channels are chosen per rule in **Regras de Captura**.

## Acceptance criteria

- Existing installs migrate: the old settings appear as one enabled rule; no
  data loss; build + startup green.
- Creating "gato → e-mail" and "pessoa → WhatsApp" rules works exactly as the
  motivating examples; rules persist and can be disabled/edited/deleted.
- A fresh capture of class X notifies exactly the channels wanted by the
  enabled rules containing X (deduplicated), and nothing when no rule matches
  or the channel master switch is off.
- All UI PT-BR; solution builds.

## Changelog

- 2026-08-29 — Initial version (v2 refactor request).
- 2026-08-29 — **Capture-alert grouping (antiflood)**: new
  `CaptureAlertSettings` singleton (`GroupingEnabled`, default on;
  `GroupWindowMinutes`, default 3 — user-configurable on the Regras de
  Captura page). With grouping on, the dispatcher no longer sends one message
  per capture: it stamps the capture (`AlertQueuedAt` + `AlertChannels`,
  resolved at queue time so later time-window changes can't drop it) and a
  web-hosted digest job sends at most one grouped summary per window per
  channel (tokenized playback link per item, first item's thumbnail inline).
  The first alert after a quiet period still goes out within ~30 s; bursts
  ride the next summary. `AlertSentAt` records delivery on both paths.
  Motivating incident: 6 e-mails inside one minute. Also raised both existing
  rules' linger from 2 s to 15 s so brief re-appearances extend one clip
  instead of spawning new capture (and alert) fragments.
  `ActiveFrom`/`ActiveTo` (`TimeOnly?`); both null = always active, otherwise
  the rule applies in `[from, to)` with midnight wrap-around when
  `to <= from` (e.g. 22:00–06:00). Example driving the change: "capture only
  from 0:00 until 6:00". Enforced twice: the worker records a class only
  while some enabled rule containing it is in-window *now* (evaluated live —
  the schedule needs no worker restart), and the dispatcher only alerts when
  the capture's `StartedAt` falls inside the matching rule's window. Rule
  dialog gains an "Somente em determinado horário" toggle + time pickers; the
  table shows the window or "Sempre".
- 2026-08-30 — **Per-tenant antiflood + capture alert log**:
  `CaptureAlertSettings` stops being a system singleton — one row per tenant
  (unique `TenantId`, `LastDigestAt` tracked per tenant; migration
  `TenantCaptureAlerts` hands the old global row to the first tenant). The
  Regras de Captura antiflood panel is now visible to tenant users
  (SuperAdmin picks the tenant in a selector). Every delivery attempt is
  recorded in `CaptureAlertLogs` (`CaptureId`, `CaptureRuleId`, `SentAt`,
  `Channel`, `Status` Success/Fail, `ErrorMessage` — also written when a
  channel is disabled or has no recipients, so silent drops are visible).
  `Capture.AlertRuleId` stores the first matching rule at queue time so the
  grouped digest can attribute its log rows. Capturas page: new "Alertas"
  icon per row opens a dialog listing the attempts (channel, rule, status,
  error) plus an "in queue" notice while a capture waits for the digest.
