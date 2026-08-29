# Management Web App — Camera Surveillance System (v1, Minimal)

## Context
The detection/recording pipeline (YOLO26n + MediaMTX) is already implemented.
This prompt covers only the **management web application** for that system.
This is the minimal first version, optimized for rapid development.

## Your task: PLAN FIRST, then implement
1. **Do not start coding immediately.** First, produce an implementation plan
   broken into sequenced spec files.
2. Save all specs in the `./specs` folder, one file per spec, numbered in
   execution order: `SPEC-00-<short-name>.md`, `SPEC-01-<short-name>.md`, etc.
3. Each spec file must contain:
   - **Objective** — what this spec delivers
   - **Scope / Out of scope**
   - **Dependencies** — which previous specs it depends on
   - **Tasks** — ordered implementation steps (checklist format)
   - **Acceptance criteria** — how to verify the spec is done
4. Suggested spec breakdown (adjust if you see a better split):
   - SPEC-00 — Solution structure, projects, layers, central package management
   - SPEC-01 — Domain modeling + EF Core + SQLite + repositories + initial migration
   - SPEC-02 — Web layer skeleton: Blazor Server + MudBlazor layout + navigation
   - SPEC-03 — Camera management (CRUD + health monitoring)
   - SPEC-04 — Capture settings
   - SPEC-05 — Captures management (browse/filter/play/delete)
   - SPEC-06 — Alert settings (configuration only)
   - SPEC-07 — System settings (SMTP + Evolution API config + QR code pairing screen)
5. After the specs are written and approved, implement them **in order**,
   one spec at a time, keeping the app buildable after each spec.

## Goal
Build a Blazor Server web application (MudBlazor UI) to manage cameras, capture
rules, recorded captures, alert settings, and system settings.

## Architecture (keep it simple)
- **Layers (2–3):**
  - `Web` — Blazor Server + MudBlazor (UI, pages, components)
  - `Core` — domain entities, enums, business rules, repository interfaces
  - `Infrastructure` — EF Core, SQLite, repository implementations
- **Blazor Server (InteractiveServer), no Web API.** Razor components inject and
  call repositories directly — fast, direct data manipulation.
- **Database:** SQLite, single file at `data/database.db`, via Entity Framework Core
  (migrations applied automatically on startup).
- **Repository pattern** over EF Core (generic base + specific repositories where needed).
- No authentication in v1.

## Features

### 1. Camera management
- CRUD for cameras: name, IP/stream URL, enabled flag.
- Health monitoring per camera: online/offline status, IP reachability, and
  latency (ping/connection time), refreshed periodically and shown in the UI
  (status badge + latency value).

### 2. Capture settings
- Which object classes should be recorded (multi-select, e.g., person, car, dog).
- Max segment duration (seconds) — when reached, the video is split into a new segment.
- **Linger time (grace period):** how many seconds after the object leaves the
  frame the capture should be finalized.
- Confidence threshold.

### 3. Alert settings (configuration only — do NOT implement sending)
- Enable/disable alerts per channel: **Email** and **WhatsApp**.
- Per-channel settings: recipients (email addresses / phone numbers),
  which object classes trigger alerts.
- The actual alert dispatching will be implemented in a future version.

### 4. Captures management
- Browse all recorded captures with filters: **date, camera, object class, person/track**.
- List with thumbnail (if available), camera name, class, start/end time, duration.
- Actions: play/download the video file, delete capture.
- Captures are read from the output folder structure and/or indexed in the database.

### 5. System settings (configuration only — do NOT implement integrations)
- **SMTP:** host, port, user, password, sender, TLS options.
- **WhatsApp (Evolution API):** base URL, API key, instance name, and a
  **QR code screen** to pair the WhatsApp session (render the QR code returned
  by Evolution API; pairing flow only — no message sending in v1).

## Domain modeling
Design the domain entities and relationships, at minimum:
- `Camera` (id, name, streamUrl, ip, enabled, createdAt)
- `CaptureSettings` (tracked classes, maxSegmentSeconds, lingerSeconds, confidenceThreshold)
- `Capture` (id, cameraId, objectClass, trackId, startedAt, endedAt, filePath, thumbnailPath)
- `AlertSettings` (channel, enabled, recipients, triggerClasses)
- `SystemSettings` (SMTP config, Evolution API config)
Adjust/extend as needed and document the model in SPEC-01.

## UI (MudBlazor)
- Layout with side navigation: Dashboard, Cameras, Captures, Capture Settings,
  Alerts, System Settings.
- Use MudBlazor components (MudTable/MudDataGrid, MudForm, MudSelect, MudChip,
  MudDialog) — clean and functional, no custom design system in v1.

## Technology
- .NET 10, Blazor Server (InteractiveServer render mode)
- MudBlazor
- EF Core + SQLite (`data/database.db`)
- Solution in `.slnx` format, `Directory.Build.props` + `Directory.Packages.props`
  (central package management), integrated into the existing solution

## Out of scope for v1
- Sending emails or WhatsApp messages (configuration screens only)
- Authentication/authorization
- Web API / external endpoints
- The detection pipeline itself (already implemented)

## Deliverables
- `./specs` folder with all numbered spec files (written **before** implementation)
- Solution/projects for the three layers wired together
- EF Core model + initial migration creating `data/database.db`
- All screens listed above, functional against the database
- Brief README section: how to run the web app