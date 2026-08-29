# CLAUDE.md

This file provides guidance to Claude Code (claude.com/claude-code) when working with this repository.

## Project

Multi-camera computer-vision prototype: connects to RTSP cameras, runs YOLO26n detection (YoloSharp / ONNX Runtime, CUDA with CPU fallback), annotates frames, republishes annotated streams through MediaMTX (WebRTC/HLS), and records rule-based MP4 clips of tracked objects. See README.md for full configuration and behavior details.

## Commands

```powershell
.\scripts\download-model.ps1          # one-time: export ./models/yolo26n.onnx
docker compose up -d                  # start MediaMTX
dotnet run --project src/CameraVision # run the pipeline (from repo root)
dotnet run --project src/CameraVision.Web # run the management web app → http://localhost:5210
dotnet build ComputerVisionTest.slnx  # build everything (fails copying CameraVision.exe while the pipeline runs — build src/CameraVision.Web instead)
```

Watch streams via `client/index.html` (plays directly from MediaMTX, never from the .NET app).

## Architecture

Solution `ComputerVisionTest.slnx` (central package management via
`Directory.Packages.props`; shared props in `Directory.Build.props`), four projects:

- `src/CameraVision.Core` — domain entities, enums, repository/service interfaces
  (no dependencies).
- `src/CameraVision.Infrastructure` — EF Core + SQLite (`data/database.db`,
  migrations auto-applied), repositories, capture importer, alert channels
  (MailKit email), Evolution API client.
- `src/CameraVision.Web` — Blazor Server (InteractiveServer) + MudBlazor 9
  management app, PT-BR UI: cookie auth (seeded `admin`/`admin2026`, login page is
  static SSR), camera CRUD + health monitor, capture browser (`/media` static
  files), settings pages, user management. Specs live in `./specs`.
- `src/CameraVision` — the detection pipeline console app (below). It does NOT
  read the web app's database yet — it keeps its own `appsettings.json`/
  `data/cameras.json`.

Pipeline (`src/CameraVision`):

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
