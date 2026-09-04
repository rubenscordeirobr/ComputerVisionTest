# CLAUDE.md

This file provides guidance to Claude Code (claude.com/claude-code) when working with this repository.

## Project

Multi-camera computer-vision prototype: connects to RTSP cameras, runs YOLO26n detection (YoloSharp / ONNX Runtime, CUDA with CPU fallback), annotates frames, republishes annotated streams through MediaMTX (WebRTC/HLS), and records rule-based MP4 clips of tracked objects. See README.md for full configuration and behavior details.

## Commands

```powershell
.\scripts\download-model.ps1          # one-time: export ./models/yolo26n.onnx
docker compose up -d                  # start MediaMTX + Evolution API (WhatsApp) + Caddy (HTTPS on 8443)
dotnet run --project src/CameraVision.Api             # processor API + media streaming → http://localhost:5220
dotnet run --project src/CameraVision.Web             # management web app → http://localhost:5210
dotnet run --project src/CameraVision.DetectionWorker # detection worker (from repo root)
dotnet build ComputerVisionTest.slnx  # build everything (fails copying a running exe — build individual projects while apps run)
```

Watch streams via `client/index.html` (plays directly from MediaMTX, never from the .NET app).

## Architecture

Solution `ComputerVisionTest.slnx` (central package management via
`Directory.Packages.props`; shared props in `Directory.Build.props`), five projects:

- `src/CameraVision.Core` — domain entities, enums, repository/service interfaces
  (no dependencies).
- `src/CameraVision.Infrastructure` — EF Core + SQLite (`data/database.db`, WAL,
  migrations auto-applied), repositories, capture importer, the alert
  dispatcher (matches capture rules and queues one `AlertDelivery` per
  recipient via the pure `AlertTargetResolver` in Core — it never sends),
  the PT-BR message composer, alert channels (MailKit email, WhatsApp via
  Evolution API), Evolution API client (QR pairing + sendText/sendMedia; the
  dockerized Evolution API is the unofficial Baileys-based WhatsApp gateway).
- `src/CameraVision.Api` — minimal API (port 5220): worker endpoints
  (`X-Api-Key`) for cameras/capture-rules/status/heartbeat/capture-ingest, and
  the media streaming service (`/media`, authorized by the web app's cookie via
  a shared Data Protection key ring at `data/keys`).
- `src/CameraVision.Web` — Blazor Server (InteractiveServer) + MudBlazor 9
  management app, PT-BR UI: cookie auth (login page is static SSR), camera CRUD
  + health monitor + health alerting (debounce, cooldown/flood cap/digest, event
  history), contacts (`/contacts`), capture rules with per-rule notification
  triggers (contacts + schedules + temporary notices, deduped per recipient)
  and antiflood windows (SPEC-16), the `AlertDeliveryHostedService` (the only
  sender of capture notifications, 10 s tick), capture browser (media
  streamed from the API), settings pages, user management. **Multi-tenant**
  (SPEC-14): data is scoped by
  `TenantId` ("Cliente" in the UI); roles User/Admin/SuperAdmin — seeded
  `admin`/`admin2026` is the tenant-less SuperAdmin (manages tenants + system
  settings), `rubens.cordeiro@live.com.br`/`test` is the first tenant's admin.
  System settings are one page per concern under the *Sistema* nav group
  (`/system/smtp`, `/system/application`, `/system/whatsapp`,
  `/system/alerts`, `/system/ai`). **WhatsApp command agent** (SPEC-17): a
  contact texts "ativar/desativar alertas" to the paired number; the API's
  `POST /api/whatsapp/webhook` (Evolution `MESSAGES_UPSERT`, `X-Webhook-Key`)
  stores a `WhatsAppCommandLog`, and `WhatsAppCommandHostedService` (web, 2 s)
  classifies it (`CommandTextRules` keyword rules, then the LLM configured on
  `/system/ai` — Gemini / Claude / DeepSeek via `ILlmClient`) and starts/ends
  the sender's own temporary notice through the shared
  `TemporaryNoticeService` (Core), replying via Evolution. SPEC-18 adds the
  read-only "status" (worker + per-camera health, `CameraStatusReport`) and
  "últimas N capturas de X" (`CaptureListReport`, tokenized watch links,
  `DetectableClassResolver` for PT-BR object words) answers. SPEC-19: voice
  notes arrive inline (`webhookBase64`), are stored under `data/inbound-audio`
  and transcribed by `ISpeechToTextClient` → `WhisperSpeechToTextClient`
  (docker-compose `whisper` / `whisper-cpu`, port 9000, settings on
  `/system/ai`) before the same text pipeline; replies quote the transcript.
  SPEC-20: a request the LLM understands but the agent cannot serve
  (`CommandIntent.Unsupported`, parsed by `LlmAnswerParser` in Core) is stored
  as an `AgentSuggestion` (SuperAdmin page `/system/suggestions`) and answered
  with the closest supported command as an offer — the log row waits in
  `AwaitingConfirmation` with the offer in `FollowUpJson`, and a "sim"
  (`CommandTextRules.TryMatchConfirmation`) runs it.
  `WorkerHealthMonitor` tracks worker liveness (SPEC-15):
  stale worker reports (>35 s) show "Sem processamento"/banners, and worker
  down/recovery fires critical admin alerts (own e-mail/WhatsApp recipients in
  `AdminAlertSettings`, history in `SystemAlertEvents`).
  Specs live in `./specs`.
- `src/CameraVision.DetectionWorker` — the detection pipeline console app
  (below). Pulls cameras + capture rules from the API at startup (falls back to
  `data/cameras.json` + local `appsettings.json` when the API is down), reports
  camera status + a global heartbeat every 30 s, and registers finished
  recordings + thumbnails via the API.

Pipeline (`src/CameraVision.DetectionWorker`):

- `Program.cs` — startup: loads `appsettings.json` + `data/cameras.json`, selects inference device (auto/cuda/cpu), spawns one `CameraPipeline` per enabled camera.
- `CameraPipeline.cs` — per-camera loop: ffmpeg RTSP decode → freshest-frame queue → shared YOLO predictor (inference serialized across cameras) → annotate → publish to MediaMTX + hand frames to recording.
- `Inference/InferenceEngine.cs` — YoloSharp predictor wrapper, CUDA probe with CPU fallback.
- `Tracking/IouTracker.cs` — simple IoU matcher assigning track IDs (no re-identification).
- `Annotation/` — box/label drawing; `ClassLabels.cs` maps COCO names to PT-BR labels.
- `Recording/` — `RecordingManager` + `TrackRecorder`: per-track MP4 segments under `output/{date}/{camera}/`, merged when multi-segment.
- `Video/Ffmpeg.cs` — ffmpeg process helpers (decode input, publish output).

Config: `appsettings.json` (repo root; relative paths resolve against its folder), cameras in `data/cameras.json`.

## Conventions

- User-facing text (client UI, frame annotations) is **PT-BR**; code, comments, logs, config values, and file names are **English**.
- Commit using the project `commit` skill: small atomic commits grouped by concern, Conventional Commits messages — never one commit with everything.
- Large binaries (models `*.pt`/`*.onnx`, `cuda-runtime/`, `output/`) are gitignored — never commit them.
