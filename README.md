# ComputerVisionTest — Multi-Camera YOLO26n Detection, Annotation & Recording

Minimal prototype that connects to multiple RTSP cameras, runs **YOLO26n** detection
([YoloSharp](https://github.com/dme-compunet/YoloSharp) / ONNX Runtime, CUDA when available),
draws annotations (box, class, confidence, tracking ID), republishes the annotated
streams through **MediaMTX**, and records rule-based MP4 clips of tracked objects.

```
RTSP cameras ──► .NET app (ffmpeg decode → YOLO26n → annotate) ──► MediaMTX ──► browser
                                     │                             (WebRTC / HLS)
                                     └─► output/…/*.mp4 (per-track segments + merged video)
```

The HTML client never talks to the .NET app — it plays the processed streams
**directly from MediaMTX** (WebRTC preferred, HLS fallback).

## Prerequisites

- .NET 10 SDK
- FFmpeg on PATH (`winget install Gyan.FFmpeg`)
- Docker (for MediaMTX and the Evolution API WhatsApp gateway)
- `uv` or Python 3.10+ (only for the one-time model export)
- Optional, for GPU inference: NVIDIA GPU + **CUDA Toolkit 12.x** + **cuDNN 9.x**
  (their `bin` folders on PATH). Without them the app automatically falls back to CPU.

## Quick start

```powershell
# 1. One-time: download/export the YOLO26n model to ./models/yolo26n.onnx
.\scripts\download-model.ps1

# 2. Start MediaMTX + Evolution API (WhatsApp) + Caddy (HTTPS)
docker compose up -d

# 3. Start the management API + web app (optional but recommended)
dotnet run --project src/CameraVision.Api   # http://localhost:5220
dotnet run --project src/CameraVision.Web   # http://localhost:5210

# 4. Run the detection worker (from the repository root)
dotnet run --project src/CameraVision.DetectionWorker

# 5. Watch: open client/index.html in a browser
```

The worker pulls cameras + capture rules from the API at startup, reports
per-camera status, and registers each finished recording (with an annotated
thumbnail) for instant alerts. When the API is down it falls back to
`data/cameras.json` + the local `recording` settings, so surveillance never
depends on the web stack. Camera/rule changes need a worker restart.

At startup the app logs the selected provider, e.g.
`Inference device: CUDA (NVIDIA GeForce RTX 5060)` or `Inference device: CPU`.

## Management web app — `src/CameraVision.Web`

Blazor Server app (MudBlazor, PT-BR UI) to manage cameras, browse recorded
captures, and configure capture rules, alerts and system settings. Data lives
in SQLite at `data/database.db` — created, migrated and seeded automatically
on startup.

```powershell
dotnet run --project src/CameraVision.Web
# then open http://localhost:5210
```

- **Login**: seeded users (hashed passwords — reset them after the first
  login in *Usuários → Redefinir senha*):
  - `admin` / `admin2026` — **SuperAdmin** (platform operator, no tenant).
  - `rubens.cordeiro@live.com.br` / `test` — **Admin** of the first tenant
    ("Rubens Cordeiro"), which owns all pre-existing data.

  All pages require a signed-in user; the same cookie also authorizes video
  streaming from the API (`/media` on port 5220), restricted to the user's
  own tenant footage.
- **Multi-tenant (SPEC-14)**: every camera, rule, capture, health event and
  recipient list belongs to a **Cliente** (tenant). Roles: *Usuário*
  (viewer), *Administrador* (manages their tenant's data + users) and
  *Superadmin* (manages tenants + system settings, sees everything). The
  SuperAdmin manages tenants in **Clientes** (create, edit,
  activate/deactivate — users of a deactivated tenant cannot sign in).
- **Câmeras**: CRUD (incl. substream URL + preferred stream) + health badges
  (ICMP ping for latency, TCP probe for online/offline), worker status column
  and a per-camera health history dialog. First run imports
  `data/cameras.json`.
- **Regras de Captura**: multiple rules per tenant — each says which classes
  are recorded, with which thresholds, and which alert channels fire (e.g.
  "gato → e-mail", "pessoa → WhatsApp"). The worker applies the union of all
  tenants' enabled rules; a capture only *alerts* through its own tenant's
  rules and recipients.
- **Capturas**: registered instantly by the worker via the API (with an
  annotated thumbnail); a background indexer reconciles pre-existing footage
  every 60 s / via *Reindexar*. Play, download and delete in the browser
  (files streamed by the API — `Api:MediaBaseUrl` in the web appsettings).
- **Alertas**: per-tenant channel recipients; capture alerts (thumbnail +
  playback link; SMTP in *Sistema*, link host in *URL pública*) and **camera
  health alerts** — offline/weak (latency threshold) with debounce, recovery
  notices, per-camera cooldown, global flood cap and an optional digest that
  groups pending events per tenant (precedence: cooldown → flood cap →
  digest; suppressed events stay in the history and ride the next digest).
  Health tuning and the capture anti-flood are system settings (SuperAdmin).
  Both channels are implemented: e-mail via SMTP and WhatsApp via the
  Evolution API (thumbnail sent as image with the alert text as caption).
- **Usuários**: admin-only user management (create, edit, deactivate, reset
  password). Tenant admins manage their own tenant; the SuperAdmin manages
  everyone and assigns tenants/roles.
- **Clientes** / **Sistema**: SuperAdmin-only — tenant management (creating a
  client also creates its admin user, atomically) and the system
  configuration (SMTP, Evolution/WhatsApp, public URL).

The API (`src/CameraVision.Api`, port 5220) serves the worker endpoints
(`X-Api-Key`, default `cameravision-dev-key` — change it in both
`src/CameraVision.Api/appsettings.json` and the root `appsettings.json`) and
streams recordings to signed-in browsers.

v1 limitations: SMTP/API secrets are stored unencrypted in SQLite (LAN use);
failed alert sends are logged, not retried; deactivating a user does not
terminate their already-open session; the worker reads cameras/rules only at
startup (restart to apply changes).

## Camera definitions — `data/cameras.json`

An array of camera objects. Only `name` and `rtspUrl` are required by the app;
`enabled` is optional (default `true`); all other fields are ignored metadata.

```json
[
  {
    "id": 1,
    "name": "camera_frente",          // used as the stream path: annotated/camera_frente
    "rtspUrl": "rtsp://192.168.3.82:554/...stream=0.sdp...",     // main stream
    "subRtspUrl": "rtsp://192.168.3.82:554/...stream=1.sdp...",  // optional substream
    "stream": "main",                 // optional: "main" (default) | "sub"
    "enabled": true                   // optional, defaults to true
  }
]
```

`"stream": "sub"` makes the app use `subRtspUrl` (low-bitrate substream) instead of
the main stream — useful for cameras on a weak Wi-Fi link, where the main stream
arrives corrupted. If `subRtspUrl` is missing, the app warns and uses the main URL.

## Configuration — `appsettings.json` (repo root)

| Key | Default | Description |
|---|---|---|
| `camerasFile` | `./data/cameras.json` | Camera list |
| `modelPath` | `./models/yolo26n.onnx` | ONNX model |
| `inferenceDevice` | `auto` | `auto` \| `cuda` \| `cpu` |
| `detection.confidenceThreshold` | `0.35` | Minimum confidence to draw a detection |
| `detection.maxProcessingWidth` | `1280` | Downscale frames wider than this (0 = off) |
| `mediamtx.publishUrlBase` | `rtsp://localhost:8554/annotated` | Streams publish to `{base}/{camera_name}` |
| `recording.trackClasses` | `["person"]` | COCO classes that trigger recording |
| `recording.confidenceThreshold` | `0.5` | Track records once it reaches this confidence |
| `recording.maxSegmentSeconds` | `60` | Max clip length; longer tracks → consecutive clips |
| `recording.outputRoot` | `./output` | Recording destination |
| `recording.lostTrackTimeoutSeconds` | `2.0` | Unseen for this long ⇒ object left the frame |

Relative paths are resolved against the folder containing `appsettings.json`.

**Language**: user-facing text is PT-BR — the client UI and the labels drawn on
frames (`src/CameraVision/Annotation/ClassLabels.cs` maps COCO names to PT-BR).
Code, comments, logs, configuration values (`trackClasses`) and recording file
names keep the English COCO class names.

## Recording behavior

When a track of a configured class reaches the recording confidence threshold:

- Its annotated frames are recorded until the object leaves the frame or the
  segment reaches `maxSegmentSeconds` (default 1 minute).
- Segments are saved as `output/{yyyy-MM-dd}/{cam_name}/{class}_{HH-mm-ss}_to_{HH-mm-ss}.mp4`.
- If the object is still present when a segment closes, the next segment starts
  immediately (consecutive clips).
- When the object finally leaves and more than one segment exists, they are
  concatenated into `{class}_{start}_to_{end}_full.mp4` (single-segment tracks
  are already complete, so no merge is produced).
- If two tracks would produce the same file name, `_track{id}` is appended.

## Viewing the streams

- **Client page**: open `client/index.html` (double-click works in most browsers;
  if yours blocks `file://` pages from fetching localhost, serve it instead:
  `python -m http.server 8000 --directory client` → http://localhost:8000).
  Camera names are read from the `DEFAULT_CAMERAS` list at the top of the file and
  can be overridden with `?cams=a,b,c&host=192.168.3.2`.
- **Direct URLs** per camera `X`:
  - WebRTC page: `http://localhost:8889/annotated/X`
  - HLS: `http://localhost:8888/annotated/X/index.m3u8`
  - RTSP: `rtsp://localhost:8554/annotated/X` (e.g. `ffplay`, VLC)

To watch from another device on the LAN, make sure `webrtcAdditionalHosts` in
`mediamtx.yml` contains this machine's IP (currently `192.168.3.2`) and use
`client/index.html?host=192.168.3.2` on that device.

## MediaMTX

`docker compose up -d` starts MediaMTX with `mediamtx.yml` mounted. Exposed ports:
8554 RTSP, 8888 HLS, 8889 WebRTC/WHEP, 8189/udp WebRTC media, 9997 API.
The `paths: all_others:` entry accepts any published path, so no per-camera
configuration is needed.

## WhatsApp (Evolution API)

The same `docker compose up -d` also starts the
[Evolution API](https://github.com/EvolutionAPI/evolution-api) v2.3.7 — an
**unofficial** WhatsApp gateway built on Baileys (WhatsApp Web protocol) — on
`http://localhost:8080`, plus its required PostgreSQL and Redis containers
(internal only, data in named Docker volumes). The global API key is set by
`AUTHENTICATION_API_KEY` in `docker-compose.yml` (default
`cameravision-evolution-key` — change it for anything beyond LAN use).

Pairing (SuperAdmin, one number for the whole system):

1. In the web app open **Sistema → WhatsApp (Evolution API)** and fill in
   URL base `http://localhost:8080`, the API key above and an instance name
   (e.g. `cameravision`), then **Salvar**.
2. Click **Gerar QR Code** and scan it from the phone (WhatsApp →
   Dispositivos conectados → Conectar dispositivo). The page polls the
   connection state and refreshes the QR every 40 s until it shows
   **Conectado**.
3. Add recipient numbers (`+5549999999999`) in **Alertas → WhatsApp**, enable
   the channel, and tick *WhatsApp* on the capture rules / health alerts that
   should use it.

The session survives container restarts (`DEL_INSTANCE=false`, instance state
in Postgres). Because this is not the official WhatsApp Business API, use a
dedicated number — numbers sending automated messages this way can be banned
by WhatsApp.

## HTTPS (Caddy)

The same `docker compose up -d` also starts a Caddy reverse proxy that
publishes the apps as `https://cameras.vemlogo.com:8443` — port 8443 because
80/443 on this host belong to unrelated services. `/media/*` routes to the
API (5220); everything else to the web app (5210), WebSockets included. The
Let's Encrypt certificate is obtained and auto-renewed through a Porkbun
**DNS-01** challenge (the `caddy/Dockerfile` builds Caddy with the
`caddy-dns/porkbun` module), so certificate issuance needs no inbound port —
only the site itself needs the router to forward 8443. Put the Porkbun API
credentials in `./.env` (`PORKBUN_API_KEY` / `PORKBUN_API_SECRET`,
gitignored) and enable **API Access** for the domain in the Porkbun
dashboard. Alert links use this origin through "URL pública" on the Sistema
page (WhatsApp only makes links clickable when the host is a real domain,
not an IP).

## GPU notes

- `YoloSharp.Gpu` bundles ONNX Runtime 1.23.x with the CUDA execution provider,
  but the CUDA Toolkit 12.x and cuDNN 9.x **system libraries are not bundled**.
  Two ways to provide them:
  - **Full install**: CUDA Toolkit 12.x + cuDNN 9.x, with their `bin` directories
    on PATH.
  - **Portable (used on this machine)**: the required DLLs (cudart, cublas/Lt,
    cufft, curand, nvJitLink from CUDA 12.9; cuDNN 9.14 for CUDA 12) are collected
    from NVIDIA's official redist CDN into `./cuda-runtime/` (gitignored), and that
    folder is added to the user PATH. Delete the folder + PATH entry to undo.
- `inferenceDevice: "auto"` probes CUDA with a warm-up inference and silently
  falls back to CPU when it fails; `"cuda"` fails hard with a clear message;
  `"cpu"` skips CUDA entirely.

## Known prototype limitations

- One shared YOLO predictor; inference across cameras is serialized (fine for a
  handful of cameras; the freshest-frame queue keeps latency bounded).
- Tracking is simple IoU matching (no re-identification after long occlusions —
  an object that disappears for more than `lostTrackTimeoutSeconds` gets a new ID
  and a new recording).
- The annotated/published frame rate equals the processing rate (wall-clock
  timestamps keep the stream real-time even when inference is slower than the
  camera frame rate).
- No authentication anywhere (MediaMTX is wide open) — LAN use only.
