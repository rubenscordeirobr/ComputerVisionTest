# SPEC-09 — Alert dispatch: Email channel live, WhatsApp pluggable

## Objective

Dispatch alerts when newly imported captures match the alert rules: a channel
abstraction (`IAlertChannel`), a dispatcher wired into the capture import, a
fully working **Email** channel (SMTP from System Settings, thumbnail embedded
in the message, link to an in-app playback page), and a registered-but-inert
**WhatsApp** channel proving the plug-in point.

## Scope

- `IAlertChannel` + `IAlertDispatcher` in `Core`, implementations in
  `Infrastructure`.
- Email sending via MailKit using `SystemSettings` SMTP values.
- Capture playback page `/captures/{id}/play` (the e-mail link target).
- Hooking dispatch into both the periodic scan and the manual **Reindexar**.
- Anti-spam guard so importing historical backlog never floods recipients.

## Out of scope

- WhatsApp/Evolution sending (the channel class exists, logs, and does
  nothing — future version fills it in).
- Retry queues/outbox: a send failure is logged and dropped (v1); an app
  restart between import and dispatch loses that alert.
- Digest/batching preferences — one e-mail per matching capture.

## Dependencies

- SPEC-01 (settings/`PublicBaseUrl`), SPEC-05 (`IndexResult.AddedCaptures`),
  SPEC-06 (per-channel rules), SPEC-07 (SMTP + URL pública forms),
  SPEC-08 (playback page sits behind login; e-mail links go through it).

## Dispatch design

- `Core`:
  - `IAlertChannel` — `AlertChannel Channel { get; }` +
    `Task<bool> TrySendAsync(CaptureAlert alert, AlertSettings rules,
    SystemSettings system, CancellationToken ct)`;
  - `CaptureAlert` — capture + camera name + PT-BR class label + absolute
    playback URL + local thumbnail path (nullable);
  - `IAlertDispatcher` — `Task DispatchAsync(IReadOnlyList<Capture>
    newCaptures, CancellationToken ct)`.
- `AlertDispatcher` (`Infrastructure`): loads `AlertSettings` (per channel) +
  `SystemSettings` once per batch; for each new capture × registered channel:
  send only when the channel is `Enabled`, has recipients, and
  `TriggerClasses` contains the capture's class (case-insensitive).
  **Recency guard**: captures with `EndedAt` older than 15 minutes are never
  alerted — a first-run import of months of backlog stays silent. Failures
  are caught and logged per capture/channel; the dispatcher never throws into
  the import loop. Because the import is idempotent (a capture is inserted
  once), each capture is considered for alerting exactly once.
- Wiring: `CaptureIndexHostedService` calls the dispatcher with
  `IndexResult.AddedCaptures` after each scan; the manual **Reindexar**
  action dispatches the same way (fire-and-forget with logging, so the UI is
  not blocked by SMTP).

## Email channel design

- MailKit (`SmtpClient`) — `System.Net.Mail.SmtpClient` is documented as not
  recommended for new development. `SmtpSecurity` maps to
  `SecureSocketOptions`: `None` → None, `StartTls` → StartTls, `SslTls` →
  SslOnConnect; authenticate only when a username is configured.
- Message (PT-BR), one per capture:
  - subject: `Alerta de captura — {classe} em {câmera}`;
  - HTML body: heading, **thumbnail embedded inline** via CID linked resource
    (never the video file; omitted gracefully when no thumbnail exists),
    camera / class / start time / duration lines, and a button-style link
    **Assistir vídeo** → `{PublicBaseUrl}/captures/{id}/play`;
  - plain-text alternative part with the same info + URL.
- `PublicBaseUrl` empty → log a warning and fall back to
  `http://localhost:5210` so links still work on the host machine.
- Skip sending (log a warning) when `SmtpHost` or sender e-mail is not
  configured.

## Playback page

- `/captures/{id:int}/play` — `[Authorize]` like every page (an e-mailed link
  first passes the login screen, then returns via `returnUrl`): video player
  (`/media/{FilePath}`), capture metadata (câmera, classe, início, duração),
  **Baixar** link and a back link to `/captures`. PT-BR "Captura não
  encontrada." when the id is unknown or the file is gone.

## Tasks

- [ ] Add `MailKit` to CPM (referenced by `Infrastructure`).
- [ ] `Core`: `IAlertChannel`, `CaptureAlert`, `IAlertDispatcher`.
- [ ] `Infrastructure`: `AlertDispatcher` (rules + recency guard + logging),
      `EmailAlertChannel` (MailKit, CID thumbnail, HTML + text parts),
      `WhatsAppAlertChannel` (logs "not implemented in v1", returns false);
      register both channels + dispatcher in DI.
- [ ] Wire dispatch into `CaptureIndexHostedService` and the **Reindexar**
      action.
- [ ] `CapturePlayback.razor` (`/captures/{id:int}/play`) as described; the
      captures table's play dialog remains unchanged.
- [ ] Manual end-to-end check with real SMTP credentials (user-provided):
      enable Email alerts for `person`, record/import a fresh capture,
      receive the e-mail, click the link, log in, watch the video.

## Acceptance criteria

- With Email enabled, recipients set, `person` in the trigger classes, SMTP +
  URL pública configured: a freshly imported `person` capture produces one
  e-mail per recipient list entry, containing the inline thumbnail (no video
  attachment) and a working playback link that opens the browser player after
  login.
- Captures of non-trigger classes, or arriving while the channel is
  disabled, send nothing.
- First-run import of an old backlog sends nothing (recency guard); repeated
  rescans never re-send for the same capture (insert-once semantics).
- SMTP failures and missing configuration are logged and never break the
  import, the UI, or the app.
- WhatsApp stays visibly configured-but-inactive: its settings persist
  (SPEC-06), no message is ever sent, and the channel class is the single
  place a future implementation plugs into.
- Build green; full solution builds; console pipeline untouched.
