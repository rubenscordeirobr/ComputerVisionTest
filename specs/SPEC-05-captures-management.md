# SPEC-05 — Captures management (import/index, browse, filter, play, delete)

## Objective

Import the MP4 recordings the pipeline writes under `output/` into the
`Capture` table (idempotent scan on startup + periodic + manual rescan, with
best-effort thumbnails and camera auto-creation), and provide a `/captures`
browser with filters (date, camera, class, track), playback, download, and
delete.

## Scope

- Filesystem capture importer (background + on-demand) that parses the
  pipeline's file naming convention and creates `Capture` entities —
  matching **or creating** the `Camera` record for each folder.
- Static file serving of the output folder so `<video>`/download links work.
- Filterable, paged capture list with play dialog, download, and delete.

## Out of scope

- Editing/re-encoding videos; retention policies; live streams.
- Indexing anything other than `*.mp4` files under the output root.
- Alert dispatch for newly imported captures (SPEC-09 hooks into the scan
  result).

## Dependencies

- SPEC-01 (Capture entity/repository), SPEC-02 (page shell), SPEC-03 (camera
  repository for match/create).

## Recording file grammar (source of truth: `TrackRecorder.cs`)

- Directory: `{outputRoot}/{yyyy-MM-dd}/{cameraName}/`
- File name regex:
  `^(?<class>.+)_(?<start>\d{2}-\d{2}-\d{2})_to_(?<end>\d{2}-\d{2}-\d{2})(?<full>_full)?(_track(?<track>\d+))?\.mp4$`
  (class names may contain spaces, e.g. `traffic light`).
- Example: `output/2026-08-29/camera_garagem/person_15-50-49_to_15-50-52.mp4`
  → camera `camera_garagem`, class `person`, `StartedAt` 2026-08-29 15:50:49,
  `EndedAt` 2026-08-29 15:50:52.
- When `end < start` the clip crossed midnight → `EndedAt` gets +1 day.
- `_full` = merged multi-segment video (`IsMerged = true`); its source
  segments also remain on disk and are indexed as their own rows (acceptable
  v1 noise — merged rows get a "completo" chip).
- `_track{id}` suffix only exists on name collisions → `TrackId` is usually
  null.
- **Skip**: files starting with `.recording_` (in-progress temp) and files
  whose `LastWriteTime` is within the last ~10 s (still being written).

## Importer design

- `Infrastructure`: `ICaptureIndexer` (interface in `Core`) + implementation:
  `Task<IndexResult> ScanAsync(ct)` — enumerate matching files, normalize
  paths relative to the output root (`/` separators), diff against
  `GetKnownFilePathsAsync()` (**idempotent** — already-imported paths are
  skipped, safe to run any number of times):
  - new files → parse metadata, `CameraId` = camera with matching name
    (case-insensitive); when no such camera exists, **create one**
    (`Name` = folder name, `StreamUrl` empty, `Enabled = false`) so captures
    always link; record `FileSizeBytes`, insert;
  - DB rows whose file no longer exists → remove;
  - thumbnails for new rows (best-effort): when `ffmpeg` is on PATH, run
    `ffmpeg -ss 1 -i <video> -frames:v 1 -vf scale=320:-2 -y <video-minus-ext>.jpg`
    (same folder — `output/` is gitignored); if that produces nothing (clip
    shorter than 1 s), retry with `-ss 0`; on success store `ThumbnailPath`,
    on failure leave null. Never let thumbnail errors fail the scan.
  - concurrent scans are serialized (semaphore) so the periodic and manual
    paths never race.
- `IndexResult` = `AddedCaptures` (the inserted `Capture` list — consumed by
  the SPEC-09 alert dispatcher) + `RemovedCount`.
- `Web`: `CaptureIndexHostedService : BackgroundService` — initial scan on
  startup, then every `CaptureIndex:IntervalSeconds` (default 60).

## Tasks

- [ ] Implement `ICaptureIndexer` + `IndexResult` + camera match-or-create +
      hosted service + config key.
- [ ] Serve recordings: `UseStaticFiles` with a `PhysicalFileProvider` over
      the resolved output root at request path `/media` (create the folder at
      startup if missing).
- [ ] `/captures` page — filter toolbar:
  - Período: `MudDateRangePicker`;
  - Câmera: `MudSelect` (distinct camera names from captures + "Todas");
  - Classe: `MudSelect` (distinct classes from captures, PT-BR labels +
    "Todas");
  - Track: `MudNumericField<int?>`;
  - buttons **Filtrar**/**Limpar**, plus **Reindexar** (runs `ScanAsync`,
    shows added/removed in a snackbar, reloads the table).
- [ ] `MudTable` with `ServerData` (server-side paging, 25/page, `StartedAt`
      desc via `ICaptureRepository.QueryAsync`): thumbnail (48 px `<img>` from
      `/media/...`, placeholder icon when null), Câmera, Classe (chip, PT-BR
      label), Track (`#n` or `—`), Início (pt-BR `dd/MM/yyyy HH:mm:ss`),
      Duração (`mm:ss`), Tamanho (MB), chip **completo** on merged rows,
      Ações.
- [ ] Ações per row:
  - **Reproduzir**: `MudDialog` with `<video controls autoplay>` pointing at
    `/media/{FilePath}` (static files give range requests → seeking works);
  - **Baixar**: link/icon button with `href="/media/{FilePath}"` and the
    `download` attribute;
  - **Excluir**: confirmation dialog → delete video file + thumbnail from
    disk + DB row → snackbar (failures reported, row kept if the file
    delete fails).

## Acceptance criteria

- Existing recordings under `output/` appear in the table after startup (or
  immediately via **Reindexar**) with correct camera, class, times, and
  duration; `_full` rows show the "completo" chip.
- Running the scan repeatedly imports nothing new (idempotent) and never
  duplicates rows.
- A recording folder whose camera is not registered produces a disabled,
  URL-less `Camera` row, and its captures link to it.
- Each filter narrows the list correctly, combined filters AND together,
  paging works.
- Play dialog streams and seeks; download saves the file; delete removes file
  + thumbnail + row, and the capture does not reappear on the next scan.
- Thumbnails render when ffmpeg is available; a placeholder icon otherwise.
- In-progress `.recording_*` temp files never appear.
- All UI text PT-BR; build green.

## Changelog

- 2026-08-29 — Capture-import refactor: importer now **creates** the Camera
  record when the folder name matches no camera (disabled, empty URL) instead
  of leaving `CameraId` null; `IndexResult` carries the inserted `Capture`
  entities so SPEC-09 can dispatch alerts for them; thumbnail extraction
  retries at `-ss 0` for very short clips; scans explicitly serialized;
  idempotency called out as an acceptance criterion.
- 2026-08-29 — v2 refactor: media is now served by the API (SPEC-11) — the
  web pages build `img`/`video`/download URLs from `Api:MediaBaseUrl` instead
  of a local `/media`. New captures normally arrive through the worker's API
  ingest with a worker-generated thumbnail (SPEC-12); this indexer stays as
  the idempotent reconciliation path (backlog, files the worker failed to
  report, deletions).
