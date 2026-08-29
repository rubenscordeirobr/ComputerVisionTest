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
- Docker (for MediaMTX)
- `uv` or Python 3.10+ (only for the one-time model export)
- Optional, for GPU inference: NVIDIA GPU + **CUDA Toolkit 12.x** + **cuDNN 9.x**
  (their `bin` folders on PATH). Without them the app automatically falls back to CPU.

## Quick start

```powershell
# 1. One-time: download/export the YOLO26n model to ./models/yolo26n.onnx
.\scripts\download-model.ps1

# 2. Start MediaMTX
docker compose up -d

# 3. Run the app (from the repository root)
dotnet run --project src/CameraVision

# 4. Watch: open client/index.html in a browser
```

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

- **Login**: initial user `admin` / password `admin2026` (stored hashed —
  reset it after the first login in *Usuários → Redefinir senha*). All pages
  and the `/media` video routes require a signed-in user.
- **Câmeras**: CRUD + health badges (ICMP ping for latency, TCP probe of the
  stream port for online/offline). On the first run the cameras from
  `data/cameras.json` are imported automatically.
- **Capturas**: recordings under `output/` are imported automatically
  (startup, every 60 s, and via *Reindexar*) by parsing the
  `{date}/{camera}/{class}_{start}_to_{end}.mp4` naming; thumbnails are
  extracted with ffmpeg. Play, download and delete work straight from the
  browser.
- **Alertas**: e-mail alerts are sent when a *fresh* capture (≤ 15 min old)
  matches the configured classes — thumbnail embedded in the message plus a
  link to the in-app playback page (`Sistema → URL pública` controls the link
  host; SMTP is configured in *Sistema*). WhatsApp is configuration-only in
  v1 (the Evolution API QR pairing screen works, sending comes later).
- **Usuários**: admin-only user management (create, edit, deactivate, reset
  password).

v1 limitations: SMTP/API secrets are stored unencrypted in SQLite (LAN use);
failed alert sends are logged, not retried; deactivating a user does not
terminate their already-open session; the detection pipeline still reads its
own `appsettings.json` (the web app's capture settings do not drive it yet).

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
