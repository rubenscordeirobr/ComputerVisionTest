# SPEC-11 — Web API project (processor endpoints + file streaming)

## Objective

New `src/CameraVision.Api` ASP.NET Core project (same Core/Infrastructure, same
SQLite database) that (a) serves the DetectionWorker: camera list, merged
capture rules, camera status updates, capture ingest with thumbnail image
upload; and (b) owns the **file streaming service** — videos/thumbnails are
served by the API, and the web app points at it via configuration.

## Scope

- `CameraVision.Api` project (minimal APIs, port 5220), added to the solution.
- Camera model extensions the worker needs: `SubStreamUrl`, `PreferredStream`
  (main/sub), `ProcessorStatus` + `ProcessorStatusAt` (reported by the worker).
- Auth: worker endpoints require an `X-Api-Key` header (shared config);
  `/media` uses the **same auth cookie as the web app** (shared Data
  Protection key ring at `data/keys` + same cookie name/application name —
  cookies are host-scoped, not port-scoped, so the browser sends them to both
  apps on the same host).
- Web app changes: local `/media` serving removed; `Api:MediaBaseUrl` in web
  `appsettings.json` (per request: the file-service location is configured in
  the web application); pages build media URLs from it. Camera dialog gains
  Substream/Preferido fields; camera table gains a worker-status indicator.
- SQLite multi-process hardening: WAL journal mode + busy timeout (two web
  hosts now share the DB file).

## Out of scope

- Any UI in the API project; user-facing endpoints beyond media (the Blazor
  app keeps talking to repositories directly).
- Worker-side changes (SPEC-12).

## Dependencies

- SPEC-10 (rules for the merged config), SPEC-08 (cookie auth being shared).

## Endpoints

| Method/Route | Auth | Behavior |
|---|---|---|
| `GET /api/processor/cameras` | API key | Enabled cameras: id, name, streamUrl, subStreamUrl, preferredStream |
| `GET /api/processor/capture-rules` | API key | Merged config: `[{name, confidenceThreshold}]` per class + max `maxSegmentSeconds` + max `lingerSeconds` |
| `POST /api/processor/cameras/{id}/status` | API key | `{status: connected\|reconnecting\|stopped, detail?}` → persists `ProcessorStatus(+At)` |
| `POST /api/processor/captures` | API key | Multipart: capture metadata JSON + optional JPEG thumbnail. Idempotent by `FilePath` (existing row: fill missing thumbnail only, no re-alert). New rows: thumbnail saved as `<video>.jpg`, capture inserted, alert dispatch invoked (recency guard applies) |
| `GET /media/{**path}` | Cookie | Static files over the output root (range requests → seeking) |

## Tasks

- [ ] Migration `CameraProcessorFields`: `SubStreamUrl` (nullable, 500),
      `PreferredStream` (string "main"/"sub", default "main"),
      `ProcessorStatus` (nullable), `ProcessorStatusAt` (nullable).
- [ ] Enable WAL + busy timeout in `AddCameraVisionData` /`DbInitializer`
      (`PRAGMA journal_mode=WAL` after migrate; `Default Timeout` in the
      connection string).
- [ ] Create `CameraVision.Api` (launchSettings http 5220; same
      `Storage`/`DataProtection` path resolution as Web; `Api:ProcessorApiKey`
      setting): DbInitializer on startup (idempotent, EF migration lock makes
      concurrent boot safe), API-key endpoint filter, endpoints above, cookie
      auth configured identically to Web, `/media` static files + 401 guard
      (moved from Web).
- [ ] Web + API: `AddDataProtection().PersistKeysToFileSystem(data/keys)`
      `.SetApplicationName("CameraVision")` so both validate the same cookie.
- [ ] Web: remove `/media` mapping/guard; add `Api:MediaBaseUrl`
      (default `http://localhost:5220`); `MediaUrls` helper service; captures
      page, playback page and player dialog use it (`img`/`video`/download —
      media-element requests carry the cookie cross-port, no CORS needed).
- [ ] Web camera dialog: **URL do substream** (optional) + **Stream
      preferido** (Principal/Substream); legacy importer maps
      `subRtspUrl`/`stream` and a one-time enrich pass fills these for
      existing cameras matched by name from `data/cameras.json`.
- [ ] Web cameras table: **Processador** column — Conectado (green) /
      Reconectando (yellow) / Parado (grey) / "—" when never reported or
      stale (> 2 min).
- [ ] `.claude/launch.json`: add an `api` configuration; solution/slnx updated.

## Acceptance criteria

- API boots on 5220; worker endpoints reject missing/wrong `X-Api-Key` (401)
  and work with it (verified with test requests).
- `/media` on the API returns 401 without the cookie and streams video (with
  seeking) for a browser session logged into the web app.
- Captures/playback pages play thumbnails and videos from `MediaBaseUrl`.
- Posting a capture twice creates one row; a fresh matching capture triggers
  the rule-based dispatch (SPEC-10).
- Both apps run simultaneously against the same SQLite file without
  `database is locked` errors under normal use.
- Solution builds; web UI stays PT-BR.

## Changelog

- 2026-08-29 — Initial version (v2 refactor request; supersedes the original
  "no Web API" constraint).
- 2026-08-29 — `/api/processor/capture-rules` no longer pre-merges per class:
  it now returns the enabled rules themselves (`classes`, `confidenceThreshold`,
  `activeFrom`/`activeTo`) plus the global max segment/linger, because
  time-of-day windows (SPEC-10) make the effective class set time-dependent —
  the worker evaluates windows live.
