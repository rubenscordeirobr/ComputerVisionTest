# SPEC-12 — DetectionWorker: rename + API integration

## Objective

Rename the pipeline console app `src/CameraVision` →
**`src/CameraVision.DetectionWorker`** (name chosen by the user) and integrate
it with the API: cameras and capture rules come from the API, per-camera
runtime status is reported back, and every finished recording is registered
through the API together with an annotated **thumbnail image** — replacing the
web app's ffmpeg thumbnail extraction for new captures.

## Scope

- Project/folder/assembly rename (git mv; `RootNamespace` stays
  `CameraVision`, so code namespaces don't churn); slnx, docs, launch config
  updated.
- `WorkerApiClient` (HttpClient + `X-Api-Key`):
  - `GET /api/processor/cameras` → replaces `data/cameras.json` as the camera
    source (**fallback**: if the API is unreachable at startup, log a warning
    and load `cameras.json` + `appsettings.json` recording config as before —
    surveillance must not depend on the API being up);
  - `GET /api/processor/capture-rules` → merged recording config (per-class
    confidence, max segment, linger) replacing the `recording` section;
  - `POST /api/processor/cameras/{id}/status` — `connected` on first decoded
    frame, `reconnecting` on ffmpeg restart, `stopped` on shutdown, plus a
    periodic heartbeat (config `api.statusIntervalSeconds`, default 30);
  - `POST /api/processor/captures` — after each segment (and merged `_full`)
    file is finalized: metadata + first-frame JPEG (320 px wide, encoded with
    the already-bundled ImageSharp from the raw BGR frame).
- Config: root `appsettings.json` gains
  `"api": { "baseUrl": "http://localhost:5220", "apiKey": …, "statusIntervalSeconds": 30 }`.
- Camera/rule changes require a worker restart (startup fetch only — documented).

## Out of scope

- Removing the local recording path — files are still written directly to
  `output/` (same machine); only metadata + thumbnails travel through the API.
- Live re-configuration without restart; WhatsApp anything.

## Dependencies

- SPEC-10 (rules), SPEC-11 (API endpoints). The running pipeline process must
  be stopped for the rename/rebuild and is restarted afterwards (user
  approved).

## Tasks

- [ ] Stop the running `CameraVision.exe`; `git mv src/CameraVision
      src/CameraVision.DetectionWorker`; rename csproj; update slnx,
      README, CLAUDE.md, `.claude/launch.json`.
- [ ] Implement `Api/WorkerApiClient.cs` + config model; wire into `Program`
      (camera + rules fetch with fallback and clear log of the source used).
- [ ] `CameraInfo` carries the database `Id`; recording config becomes
      per-class confidence map + global segment/linger.
- [ ] `CameraPipeline`: status transitions + heartbeat (fire-and-forget with
      logging, never blocking the frame loop).
- [ ] `TrackRecorder`/`RecordingManager`: keep the first frame of each
      segment; on finalize, encode JPEG and post segment (and `_full`) to the
      API (failures logged; files remain on disk for the indexer to reconcile).
- [ ] Rebuild, start the renamed worker, confirm: cameras fetched from API,
      status visible in the web Cameras screen, a new capture appears with
      the worker-provided thumbnail without ffmpeg extraction.

## Acceptance criteria

- `dotnet run --project src/CameraVision.DetectionWorker` works from the repo
  root; old project path is gone from the solution.
- With the API up: worker logs "cameras/rules loaded from API", Cameras screen
  shows **Processador: Conectado**, and new recordings appear in Capturas
  (thumbnail included) within seconds of the track ending — no 60 s indexer
  wait, alerts fire per SPEC-10 rules.
- With the API down: worker still starts and records using the local JSON
  fallback (warning logged).
- The indexer (SPEC-05) no longer double-imports worker-posted files
  (idempotent by path) and remains the reconciliation path for pre-existing
  footage.

## Changelog

- 2026-08-29 — Initial version (v2 refactor request; name
  `CameraVision.DetectionWorker` picked by the user over
  Processor/YoloProcessor/EdgeAgent).
- 2026-08-29 — The worker self-registers the repo-local `./cuda-runtime`
  folder on its process PATH before loading the model, so CUDA inference works
  regardless of the launching environment (previously it silently fell back to
  CPU when launched without the user PATH).
- 2026-08-29 — Recording decisions honor the rules' time-of-day windows
  (SPEC-10): per frame, a track is recordable only if some fetched rule
  containing its class is active at that moment (min confidence among the
  active ones). Windows are evaluated live; only rule-list/camera *edits*
  still require a restart. The JSON fallback keeps the old always-on
  behavior.
