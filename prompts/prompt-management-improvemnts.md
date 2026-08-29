## Your task: CREATE THE SPECS FIRST, then implement
 

## IMPORTANT — UI/UX language: Brazilian Portuguese (pt-BR)
- **All user-facing text must be in Brazilian Portuguese**: pages, menus,
  labels, buttons, table headers, dialogs, validation messages, tooltips,
  empty states, and notification content (email subject/body, future WhatsApp
  messages, digest messages).
- This applies to the **new screens AND all existing screens** — add a spec
  (or changelog entries) to translate any UI text currently in English.
- Formatting must follow pt-BR conventions: dates `dd/MM/yyyy`, 24h time,
  decimal comma (culture `pt-BR` configured in the app).
- MudBlazor localization: configure component texts (e.g., data grid paging,
  date pickers) for pt-BR where supported.
- Code, entities, database fields, file names, and specs remain in **English**
  — only the user-facing layer is translated.
- No multi-language/resx infrastructure needed in v1 — pt-BR hardcoded is
  acceptable; just keep texts easy to locate for future localization.

## New feature: camera health alerts

### A. Alert conditions
- Trigger an alert when a camera becomes:
  - **Offline** — unreachable / stream down, or
  - **Weak** — reachable but degraded (latency above a configurable threshold,
    e.g., > 500 ms, or repeated intermittent failures).
- To avoid false positives, a state change must persist for a configurable
  number of consecutive checks (e.g., 3 failed checks) before alerting.
- Also send a **recovery notification** when the camera comes back to healthy
  (configurable on/off).
- Reuse the existing alert channels (`IAlertChannel`): Email implemented,
  WhatsApp remains configuration-only.
- Notification texts in pt-BR, e.g.:
  - `Câmera "Garagem" ficou offline às 14:02.`
  - `Câmera "Portão" com sinal fraco (latência 820 ms) às 14:05.`
  - `Câmera "Garagem" voltou ao normal às 14:10.`

### B. Anti-flood controls (per camera + global)
The goal is to never spam the user with repeated notifications:
1. **Cooldown period:** minimum interval between notifications for the same
   camera/condition (e.g., after alerting "camera Garagem offline", stay silent
   about it for N minutes, even if it stays offline). Configurable in minutes.
2. **Max notifications per period (flood cap):** a global limit such as
   "at most X notifications per Y minutes". When the cap is reached, stop
   sending individual notifications and hold them.
3. **Digest mode (grouping):** optional setting — instead of (or after
   hitting the flood cap) sending individual messages, **group pending
   notifications and send them as a single digest message** at a configurable
   interval (e.g., every 15 minutes: `Resumo: 3 eventos — Garagem offline
   14:02; Portão sinal fraco 14:05; Garagem normalizada 14:10.`).
- Precedence must be documented in the spec: cooldown → flood cap → digest.
- Suppressed/held events must not be lost: they appear in the next digest.

### C. Configuration (Alert Settings screen — "Alertas")
Extend the alert settings with a "Saúde das câmeras" section (all labels in
pt-BR):
- Enable/disable camera health alerts (global + per camera override if simple).
- Weak-signal latency threshold (ms) and consecutive-checks count.
- Recovery notification on/off.
- Cooldown period (minutes).
- Flood cap: max notifications / period (minutes).
- Digest mode: off | on (with digest interval in minutes).
- Channels and recipients (reuse the existing recipients configuration).

### D. History
- Persist camera health events (camera, condition, occurredAt, notifiedAt,
  suppressed/grouped flag) so the digest can be built and the user can audit
  what happened even when notifications were suppressed.
- Optional simple UI: a "Histórico de saúde" list per camera or a tab in the
  Cameras screen.
