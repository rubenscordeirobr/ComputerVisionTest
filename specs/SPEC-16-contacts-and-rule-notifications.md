# SPEC-16 — Contacts, rule notifications, temporary notices and per-rule antiflood

## Problem

A capture rule could only say "e-mail" / "WhatsApp"; every notification of a tenant
went to the same recipient lists, the antiflood window was one setting per tenant,
and the API sent e-mails synchronously inside the capture-ingest request. The owner
needs different people at different times ("Pessoas: e-mail always; 00:00–06:00
Mon–Fri → contact 1 on WhatsApp; weekends → contact 1; 18:00–00:00 → contact 2"), a
temporary "notify me until I'm back" switch that never doubles messages, and a
grouping window per rule ("cats and dogs every 30 min, people every 3 min").

## Contacts

- `Contact` (per tenant): name (unique per tenant), optional e-mail, optional WhatsApp
  number (stored normalized as `+digits`), `NotifyCameraHealth`. Page **Contatos**
  (`/contacts`, every role; a SuperAdmin sees every tenant).
- `RecipientNormalizer` (Core): e-mail trim + lower-case, phone digits only (10–15)
  → `+digits`. Used for storage and as the dedupe key.
- Camera-health alerts go to the contacts flagged `NotifyCameraHealth`;
  `AlertSettings` keeps only the per-tenant, per-channel **master switch**.
- Deleting a contact removes it from every trigger (JSON id list) in the same
  transaction; its past deliveries keep the address (`ContactId` becomes null).

## Notification triggers

- `AlertTrigger` (child of `CaptureRule`, cascade): channel, `ContactIds`, `Kind`
  (Always / Weekly / Temporary — an editor discriminator only), `Days` (flags,
  Monday = 1 … Sunday = 64), `StartTime`/`EndTime` (null = all day),
  `ActiveFrom`/`ExpiresAt`, `Enabled`.
- `IsActiveAt(moment)` evaluates every constraint uniformly. A window crossing
  midnight belongs to the day it **started**: "Sex 22:00–06:00" covers Saturday
  02:00, "Seg–Sex 00:00–06:00" does not cover Saturday 03:00; `End == 00:00` means
  until the end of the day; equal times = 24 h from `Start`.
- Schedules are evaluated at the capture's `StartedAt` (who was on duty when it
  happened), in server local time like the rest of the app.
- `AlertTargetResolver` (Core, pure): matching rules (class + rule window) ordered by
  `GroupWindowMinutes` then Id → active triggers → contacts → normalized address; each
  (channel, address) pair is claimed **once** across triggers and rules — overlapping
  triggers yield one message, and the rule with the shortest window claims first.
- Rule dialog: section **Notificações** (table + nested `AlertTriggerDialog`: channel,
  the contacts that have an address for it, Sempre / Dias e horários / Temporário),
  saved with the rule through `ReplaceTriggersAsync`. `UpdateAsync` writes scalars
  only, so the inline "Ativa" toggle can never wipe triggers. `NotifyEmail` /
  `NotifyWhatsApp` are gone.

## Temporary notices

- **Aviso temporário** on `/capture-settings`: channel, contacts, end ("até eu
  desativar" or date/time) and rules (default: every enabled rule) → one `Temporary`
  trigger per rule with `ActiveFrom = now`. Each rule row shows a warning chip
  "Temporário · canal" whose X deletes the trigger; a banner offers "Encerrar todos".
  Expired ones show "Expirada" in the rule dialog and can be deleted there.

## Antiflood per rule

- `CaptureRule.GroupWindowMinutes`: 0 = **Imediato** (each capture is its own
  message); N = one summary per recipient per N minutes. `CaptureAlertSettings` is
  gone.
- No `LastDigestAt` column: a rule's last attempt is `MAX(SentAt)` of its deliveries
  (Sent and Failed alike, so a broken channel never loops).

## Delivery outbox

- `AlertDelivery`: one row per capture × rule × channel × recipient
  (`Pending` → `Sent` / `Failed`; `QueuedAt`; `SentAt` = attempt time;
  `ErrorMessage`; `ContactId` SetNull). Replaces `CaptureAlertLogs` and the
  `Capture.Alert*` columns.
- `AlertDispatcher` (API ingest, web indexer, manual reindex) only matches and
  enqueues; the API no longer registers alert channels.
- `AlertDeliveryHostedService` (web, every 10 s): pending rows grouped by rule → a
  rule inside its window is skipped → per (channel, recipient): master switch off →
  Failed "Canal desativado…"; window 0 → individual messages; window N → one summary
  (a single capture goes out as the individual message) → rows are marked right after
  each send. One message per recipient (WhatsApp always was; e-mail now too).
- `IAlertChannel.TrySendAsync(message, recipients, system)` takes the recipients
  explicitly.
- Capture dialog **Notificações da captura**: hora, canal, destinatário (contact name
  + address), regra, status (Na fila / Enviado / Falhou), detalhe.

## Migration `RuleNotifications`

Hand-ordered (creates → raw SQL → drops, because the SQLite provider defers table
rebuilds to the end): tenant recipients → one contact each, flagged for health alerts;
`NotifyEmail` / `NotifyWhatsApp` → one Always trigger per rule and channel over those
contacts; the tenant antiflood window → every rule of the tenant; `CaptureAlertLogs`
→ deliveries (recipient unknown). Captures still queued at upgrade time are not
converted.

## UI routes

| Route               | Change                                                               |
| ------------------- | -------------------------------------------------------------------- |
| `/contacts`         | New: contacts CRUD with the health-alert flag                        |
| `/capture-settings` | Notificações + Antiflood columns, temporary notices, no tenant panel |
| `/alerts`           | Channel master switches only (recipients live in Contatos)           |
| `/captures`         | Bell → "Notificações da captura" (per-recipient status)              |

Tests: `tests/CameraVision.Core.Tests` (xunit) cover `AlertTrigger.IsActiveAt`,
`RecipientNormalizer` and `AlertTargetResolver`.
