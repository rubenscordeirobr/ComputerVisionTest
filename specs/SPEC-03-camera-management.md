# SPEC-03 — Camera management (CRUD + health monitoring)

## Objective

Full camera CRUD on `/cameras` plus periodic health monitoring (online/offline
+ latency) surfaced live in the UI, and a one-time import of the existing
`data/cameras.json` so the app starts populated.

## Scope

- Camera list, create/edit dialog, delete with confirmation, inline
  enable/disable.
- Background health monitor (ping + TCP connect) with in-memory status and a
  change event the UI subscribes to.
- One-time legacy import from `data/cameras.json` when the table is empty.

## Out of scope

- Live stream preview (the existing `client/index.html` already covers
  viewing; may link later).
- Editing `data/cameras.json` back (the pipeline keeps reading its own file in
  v1; DB and JSON are independent).

## Dependencies

- SPEC-01 (Camera entity/repository), SPEC-02 (page shell).

## Health monitoring design

- `Core`: enum `CameraStatus { Unknown, Online, Offline, Disabled }`; record
  `CameraHealth(int CameraId, CameraStatus Status, long? PingMs, long?
  ConnectMs, DateTime CheckedAt)`; interface `ICameraHealthService` with
  `CameraHealth? TryGet(int cameraId)` and `event Action? Changed`.
- `Web`: `CameraHealthMonitor : BackgroundService` implementing the interface
  (registered as singleton, exposed via both contracts). Every
  `HealthCheck:IntervalSeconds` (config, default 15):
  - reload cameras from the repository (picks up CRUD changes each cycle);
  - `Enabled == false` → status `Disabled`, skip probing;
  - target host = `IpAddress` when set, else `new Uri(StreamUrl).Host`; when
    neither yields a host (empty `StreamUrl` on cameras auto-created by the
    capture import), the camera is not probed — status stays `Unknown` and
    the UI shows a grey **Sem stream** chip;
  - port = URI port when present, else 554;
  - ICMP `Ping` (2 s timeout) → `PingMs` (latency; failure tolerated — ICMP
    may be blocked);
  - TCP connect to `host:port` (3 s timeout) → success = `Online` +
    `ConnectMs`, failure = `Offline`;
  - probe cameras concurrently; store snapshots in a
    `ConcurrentDictionary`; raise `Changed` once per cycle.
- UI latency display: `PingMs` preferred, else `ConnectMs`.

## Tasks

- [ ] One-time import at startup: if the Cameras table is empty and
      `Storage:LegacyCamerasFile` (default `../../data/cameras.json`) exists,
      import `name`, `rtspUrl` → `StreamUrl`, `ipAddress`, `enabled`;
      best-effort with logging, never fatal.
- [ ] `/cameras` page: `MudTable` with columns Nome, Stream (truncated URL +
      tooltip with the full value), IP, Ativa (inline `MudSwitch`, saves
      immediately), Status (`MudChip`: verde **Online**, vermelho **Offline**,
      cinza **Verificando…**/**Desativada**), Latência (`{n} ms` or `—`),
      Ações (editar/excluir icon buttons); toolbar button **Nova câmera**.
- [ ] `CameraDialog` (`MudDialog` + `MudForm`) for create/edit: Nome
      (required, unique — validated against the repo), URL do stream
      (required, must parse as `rtsp`/`http`/`https` URI), IP (optional,
      `IPAddress.TryParse` or hostname), Ativa. PT-BR validation messages.
- [ ] Delete: `MudMessageBox` confirmation (PT-BR); note that existing
      captures keep their rows (FK set-null).
- [ ] Implement `CameraHealthMonitor` + registration; wire `Changed` →
      `InvokeAsync(StateHasChanged)` in the page (unsubscribe on dispose).
- [ ] Config: `HealthCheck:IntervalSeconds` in `appsettings.json` (default
      15).

## Acceptance criteria

- Fresh database: the 3 existing cameras appear after first start (imported).
- Create/edit/delete/toggle persist across app restarts; validation blocks
  empty name, duplicate name, and invalid URL, with PT-BR messages.
- Status chips converge to reality within ~2 intervals: reachable camera →
  Online with latency; unplugged/unknown IP → Offline; disabled camera →
  Desativada and is not probed.
- Status updates arrive without manual page refresh.
- A camera with no IP and no stream URL shows **Sem stream** and is never
  probed.
- Build green.

## Changelog

- 2026-08-29 — Capture-import refactor: the import (SPEC-05) may auto-create
  cameras with an empty `StreamUrl` (disabled). Health monitoring skips such
  cameras (grey **Sem stream** chip) instead of failing to parse a URL. The
  camera dialog still requires a stream URL — completing auto-created cameras
  is the intended path to enable them.
