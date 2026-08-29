# SPEC-07 — System settings (SMTP + Evolution API + WhatsApp QR pairing)

## Objective

`/system-settings` page persisting SMTP and Evolution API configuration
(singleton `SystemSettings` row), plus a WhatsApp pairing panel that renders
the QR code returned by the Evolution API and tracks connection state. No
message sending.

## Scope

- SMTP configuration form (no test-send).
- Evolution API configuration form.
- Pairing flow: request QR → render → poll connection state until connected.
- Final README polish (web app section: settings + pairing + limitations).

## Out of scope

- Sending email or WhatsApp messages (future version).
- Secret encryption — values stay plaintext in SQLite (documented v1
  limitation: no auth, LAN prototype).

## Dependencies

- SPEC-01 (SystemSettings entity/repository), SPEC-02 (page shell).

## Evolution API client design

- `Core`: `IEvolutionApiClient` +
  `EvolutionQr(string? Base64Image, string? PairingCode)` and
  `EvolutionState(EvolutionConnection Connection, string? Error)` with enum
  `EvolutionConnection { Open, Connecting, Closed, Error }`:
  - `Task<EvolutionQr> ConnectAsync(SystemSettings s, CancellationToken ct)`
  - `Task<EvolutionState> GetStateAsync(SystemSettings s, CancellationToken ct)`
- `Infrastructure` implementation via `IHttpClientFactory`, header
  `apikey: {EvolutionApiKey}`, 10 s timeout, base = `EvolutionBaseUrl`:
  - `GET /instance/connect/{instanceName}` → JSON with `base64` (data-URI
    PNG) and/or `code`;
  - on 404 (instance does not exist): `POST /instance/create` with
    `{ "instanceName": ..., "qrcode": true, "integration":
    "WHATSAPP-BAILEYS" }`, then retry connect once;
  - `GET /instance/connectionState/{instanceName}` → `instance.state` ∈
    `open | connecting | close`;
  - every failure (network, non-2xx, bad JSON) maps to
    `EvolutionState(Error, message)` / thrown-free results — the UI never
    sees exceptions.

## Tasks

- [ ] Page section **E-mail (SMTP)** — `MudForm`: Servidor, Porta
      (`MudNumericField`, 1–65535), Usuário, Senha (`InputType.Password` with
      reveal toggle), E-mail do remetente (validated), Nome do remetente,
      Segurança (`MudSelect`: **Nenhuma** / **STARTTLS** / **SSL/TLS**);
      **Salvar** → snackbar.
- [ ] Page section **Aplicação** — `MudForm`: **URL pública**
      (`SystemSettings.PublicBaseUrl`, absolute http/https URI, e.g.
      `http://192.168.3.2:5210`) with a PT-BR helper text explaining it is
      used to build the playback links inside alert e-mails (SPEC-09);
      **Salvar** → snackbar.
- [ ] Page section **WhatsApp (Evolution API)** — `MudForm`: URL base
      (absolute http/https URI), API Key (password-style), Nome da instância;
      **Salvar** → snackbar.
- [ ] Pairing panel **Parear WhatsApp** (below the Evolution form):
  - **Gerar QR Code** button (disabled until base URL + API key + instance
    are saved) → `ConnectAsync` → render `<img>` from base64 + pairing code
    (when provided);
  - poll `GetStateAsync` every 3 s while the panel is active; status chip:
    verde **Conectado** (`open`, hides the QR), amarelo **Aguardando leitura
    do QR** (`connecting`), cinza **Desconectado** (`close`), vermelho
    **Erro** + message;
  - auto-refresh the QR every ~40 s while not connected (QR expiry);
    **Atualizar QR** button for manual refresh;
  - polling/timers cancelled on page dispose (`IAsyncDisposable` + CTS).
- [ ] Caption on the page (PT-BR): credentials are stored locally without
      encryption; sending will arrive in a future version.
- [ ] README: extend the web app section — settings pages, Evolution pairing
      summary, v1 limitations (plaintext secrets, no auth, no sending).

## Acceptance criteria

- SMTP and Evolution values persist across restarts; SMTP form validates
  port range and sender email with PT-BR messages.
- With a reachable Evolution API instance: QR renders, state chip follows the
  real state, and reaches **Conectado** after scanning (manual verification).
- With no/unreachable API: clicking **Gerar QR Code** shows the **Erro** state
  with a readable PT-BR message — no unhandled exception, circuit stays
  alive.
- **URL pública** persists and is consumed by SPEC-09 when building e-mail
  links.
- This spec itself adds no message-sending code (Email dispatch is SPEC-09;
  WhatsApp sending stays out of v1); build green; full solution still builds
  and the console pipeline is untouched.

## Changelog

- 2026-08-29 — Alerts refactor: added the **Aplicação / URL pública** section
  (`SystemSettings.PublicBaseUrl`, new field from SPEC-01) because alert
  e-mails need an absolute link to the capture playback page. SMTP settings
  are now actually consumed by the Email alert channel (SPEC-09) instead of
  being config-only.
