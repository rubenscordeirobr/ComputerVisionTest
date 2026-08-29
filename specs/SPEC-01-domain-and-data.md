# SPEC-01 — Domain model, EF Core + SQLite, repositories, initial migration

## Objective

Model the domain in `CameraVision.Core`, implement persistence in
`CameraVision.Infrastructure` (EF Core + SQLite at `data/database.db`,
repository pattern), create the initial migration, and have the web app apply
migrations and seed defaults automatically at startup.

## Scope

- Entities, enums, and repository **interfaces** in `Core`.
- `AppDbContext`, value converters, repository **implementations**, DI
  registration, and the `InitialCreate` migration in `Infrastructure`.
- Automatic `Database.Migrate()` + seeding on web app startup.
- `DetectableClasses` reference list in `Core` (COCO class names + PT-BR
  labels, copied from `src/CameraVision/Annotation/ClassLabels.cs`) — used by
  later specs for every class dropdown. Duplication with the console app is
  accepted in v1 (unifying is future work).
- `.gitignore` entry for the database file.

## Out of scope

- Any UI (SPEC-02+). The pipeline console app does **not** read these tables
  in v1.
- Seeding the `admin` user (SPEC-08 does it, together with the password
  hasher).

## Dependencies

- SPEC-00 (projects and CPM exist).

## Domain model

All timestamps are stored as **local time** (`DateTime`), matching the
pipeline's local-time recording file names.

| Entity | Fields | Notes |
|---|---|---|
| `Camera` | `Id`, `Name` (required, max 100, unique), `StreamUrl` (max 500, **may be empty** — cameras auto-created by the capture import have no URL, see SPEC-05), `IpAddress` (optional, max 64), `Enabled` (default true), `CreatedAt` | |
| `CaptureSettings` | `Id` (fixed 1), `TrackedClasses` (`List<string>`, JSON column), `MaxSegmentSeconds` (int), `LingerSeconds` (double), `ConfidenceThreshold` (double) | Singleton row. Seed = pipeline defaults: `["person"]`, 60, 2.0, 0.5 |
| `Capture` | `Id`, `CameraId` (nullable FK → Camera, `OnDelete=SetNull`), `CameraName` (from folder), `ObjectClass`, `TrackId` (nullable int), `StartedAt`, `EndedAt`, `FilePath` (relative to output root, `/` separators, unique), `ThumbnailPath` (nullable), `IsMerged` (bool, `_full` files), `FileSizeBytes` (long), `IndexedAt` | Indexes: `StartedAt`, `CameraName`, `ObjectClass`; unique `FilePath` |
| `AlertSettings` | `Id`, `Channel` (enum `AlertChannel { Email, WhatsApp }`, unique, stored as string), `Enabled`, `Recipients` (`List<string>`, JSON), `TriggerClasses` (`List<string>`, JSON) | Seed one row per channel, disabled, empty lists |
| `SystemSettings` | `Id` (fixed 1), `SmtpHost`, `SmtpPort` (default 587), `SmtpUsername`, `SmtpPassword`, `SmtpSenderEmail`, `SmtpSenderName`, `SmtpSecurity` (enum `{ None, StartTls, SslTls }`, default StartTls, stored as string), `PublicBaseUrl` (base URL used to build links in alert emails, e.g. `http://192.168.3.2:5210`), `EvolutionBaseUrl`, `EvolutionApiKey`, `EvolutionInstanceName` | Singleton row, flat columns; strings default empty. Secrets stored in plaintext — known v1 limitation (no encryption, LAN prototype) |
| `AppUser` | `Id`, `Username` (required, max 64, unique), `DisplayName` (optional, max 100), `PasswordHash` (required), `IsAdmin` (bool), `IsActive` (default true), `CreatedAt` | Passwords only ever stored hashed. `admin` user seeded by SPEC-08 |

## Repository design

Blazor Server circuits are long-lived, so **never** inject a scoped
`DbContext` into components. Register `AddDbContextFactory<AppDbContext>` and
have every repository method create a short-lived context from
`IDbContextFactory<AppDbContext>`.

Interfaces in `Core` (implementations in `Infrastructure`):

- `ICameraRepository` — `GetAllAsync`, `GetByIdAsync`, `AddAsync`,
  `UpdateAsync`, `DeleteAsync`, `AnyAsync`.
- `ICaptureRepository` — `QueryAsync(CaptureFilter)` returning
  `(IReadOnlyList<Capture> Items, int TotalCount)` ordered by `StartedAt`
  desc; `GetByIdAsync`, `AddRangeAsync`, `DeleteAsync`,
  `GetKnownFilePathsAsync()`, `RemoveByFilePathsAsync(paths)`,
  `GetDistinctCameraNamesAsync()`, `GetDistinctClassesAsync()`, count helpers
  for the dashboard.
  `CaptureFilter`: `DateFrom?`, `DateTo?`, `CameraName?`, `ObjectClass?`,
  `TrackId?`, `Skip`, `Take`.
- `ISettingsRepository` — `GetCaptureSettingsAsync` / `SaveCaptureSettingsAsync`,
  `GetAlertSettingsAsync(channel)` / `SaveAlertSettingsAsync`,
  `GetSystemSettingsAsync` / `SaveSystemSettingsAsync`.
- `IUserRepository` — `GetAllAsync`, `GetByIdAsync`, `GetByUsernameAsync`,
  `AddAsync`, `UpdateAsync`, `UsernameExistsAsync`. (No delete — users are
  deactivated, not removed.)

## Tasks

- [ ] Add `Microsoft.EntityFrameworkCore.Sqlite` +
      `Microsoft.EntityFrameworkCore.Design` (latest stable 10.0.x) to
      `Directory.Packages.props`; reference them from `Infrastructure`
      (`Design` with `PrivateAssets=all`).
- [ ] Create local tool manifest (`dotnet new tool-manifest`) and install
      `dotnet-ef` as a local tool.
- [ ] `Core`: entities (including `AppUser`), enums, `CaptureFilter`,
      repository interfaces (including `IUserRepository`),
      `DetectableClasses` (name + PT-BR label for the 80 COCO classes).
- [ ] `Infrastructure`: `AppDbContext` with converters (`List<string>` ↔ JSON
      with a proper `ValueComparer`; enums as strings), constraints and
      indexes from the table above.
- [ ] `Infrastructure`: repository implementations over the context factory.
- [ ] `Infrastructure`: `IDesignTimeDbContextFactory<AppDbContext>` (dummy
      SQLite path) so `dotnet ef` works without the web app.
- [ ] `Infrastructure`: DI extension
      `AddCameraVisionData(this IServiceCollection, string dbPath)`.
- [ ] Migration: `dotnet ef migrations add InitialCreate --project
      src/CameraVision.Infrastructure`.
- [ ] `Web`: `appsettings.json` gets `"Storage": { "DatabasePath":
      "../../data/database.db", "OutputRoot": "../../output" }`; resolve
      relative paths against `ContentRootPath` into an options/paths helper.
- [ ] `Web` startup: ensure the db directory exists → `Database.Migrate()` →
      seed `CaptureSettings`, `SystemSettings`, and both `AlertSettings` rows
      when missing (`DbInitializer`, extended by SPEC-08 for the admin user).
- [ ] `.gitignore`: add `data/database.db*` (covers `-wal`/`-shm`).

## Acceptance criteria

- `dotnet build ComputerVisionTest.slnx` succeeds; `Migrations/` folder exists
  in `Infrastructure`.
- Running the web app creates `data/database.db` with the six tables and the
  four settings seed rows (verifiable with any SQLite client or EF logging).
- `git status` does not show `data/database.db`.
- Repositories are the only data-access path exposed to `Web` (no `DbContext`
  usage outside `Infrastructure`).

## Changelog

- 2026-08-29 — Auth/alerts refactor: added `AppUser` entity +
  `IUserRepository` (SPEC-08 needs them; since implementation had not started,
  the user table rides the `InitialCreate` migration instead of a follow-up
  one). Added `SystemSettings.PublicBaseUrl` (alert emails need an absolute
  playback link, SPEC-09). `Camera.StreamUrl` is no longer required at the
  entity level (the capture import auto-creates URL-less cameras, SPEC-05).
  Admin seeding explicitly deferred to SPEC-08.
- 2026-08-29 — v2 refactor: `CaptureSettings` singleton replaced by the
  `CaptureRules` table and `AlertSettings.TriggerClasses` dropped (SPEC-10);
  `Camera` gains `SubStreamUrl`/`PreferredStream`/`ProcessorStatus(At)`
  (SPEC-11); new `HealthAlertSettings` + `CameraHealthEvent` tables (SPEC-13).
  Each change ships in its own follow-up migration. SQLite runs in WAL mode
  with a busy timeout because Web and Api now share the database file.
