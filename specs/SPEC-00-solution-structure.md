# SPEC-00 — Solution structure, projects, layers, central package management

## Objective

Turn the repository into a multi-project `.slnx` solution with central package
management, and add the three empty layer projects for the management web app
(`Core`, `Infrastructure`, `Web`) wired together by project references — while
keeping the existing `src/CameraVision` console app building and running
exactly as before.

## Scope

- `ComputerVisionTest.slnx` at the repo root containing all four projects.
- `Directory.Build.props` (repo root): shared `TargetFramework=net10.0`,
  `ImplicitUsings`, `Nullable`.
- `Directory.Packages.props` (repo root): `ManagePackageVersionsCentrally=true`;
  all package versions live here (starting with the existing
  `YoloSharp.Gpu 6.1.0`).
- New projects (no feature code yet):
  - `src/CameraVision.Core` — class library, no dependencies.
  - `src/CameraVision.Infrastructure` — class library, references `Core`.
  - `src/CameraVision.Web` — Blazor Web App (empty template, Interactive
    Server), references `Core` and `Infrastructure`.
- Existing `src/CameraVision/CameraVision.csproj` adjusted for CPM (remove
  `Version` attributes; drop properties now inherited from
  `Directory.Build.props`).

## Out of scope

- MudBlazor, EF Core, or any NuGet package beyond what already exists
  (added by SPEC-01/SPEC-02).
- Any domain/UI code.

## Dependencies

None (first spec). Requires .NET SDK 10.0.x (10.0.400 is installed).

## Global conventions (apply to this and every later spec)

- **Language**: all user-facing UI text is **PT-BR**; code, comments, logs,
  config keys/values, routes, and file names are **English** (same rule the
  pipeline already follows).
- **Buildable checkpoints**: after each spec, `dotnet build
  ComputerVisionTest.slnx` must succeed and the web app must start.
- Large/generated files (`data/database.db`, `output/`) are never committed.

## Tasks

- [ ] Create `Directory.Build.props` with `TargetFramework=net10.0`,
      `ImplicitUsings=enable`, `Nullable=enable`.
- [ ] Create `Directory.Packages.props` with
      `ManagePackageVersionsCentrally=true` and `YoloSharp.Gpu` `6.1.0`.
- [ ] Edit `src/CameraVision/CameraVision.csproj`: remove the `Version`
      attribute from `PackageReference` and the properties now provided by
      `Directory.Build.props` (keep `OutputType`, `RootNamespace`).
- [ ] `dotnet new classlib -o src/CameraVision.Core` (delete `Class1.cs`,
      strip redundant properties from the csproj).
- [ ] `dotnet new classlib -o src/CameraVision.Infrastructure`; add project
      reference to `Core`.
- [ ] `dotnet new blazor --empty --interactivity Server -o
      src/CameraVision.Web`; add project references to `Core` and
      `Infrastructure`; strip redundant csproj properties.
- [ ] Create `ComputerVisionTest.slnx` (hand-authored XML `<Solution>` with
      four `<Project Path=.../>` entries, or `dotnet new`/`dotnet sln` when the
      SDK supports creating slnx directly) and verify with
      `dotnet sln ComputerVisionTest.slnx list`.
- [ ] `dotnet build ComputerVisionTest.slnx` — fix any CPM (NU1008/NU1010)
      errors.

## Acceptance criteria

- `dotnet sln ComputerVisionTest.slnx list` shows the 4 projects.
- `dotnet build ComputerVisionTest.slnx` succeeds with no NuGet CPM warnings.
- No `PackageReference` in any csproj carries a `Version` attribute.
- `dotnet run --project src/CameraVision` still starts the pipeline (no
  behavior change to the console app).
- `dotnet run --project src/CameraVision.Web` serves the template placeholder
  page.

## Changelog

- 2026-08-29 — v2 refactor: the solution later gains `CameraVision.Api`
  (SPEC-11) and the console app is renamed to `CameraVision.DetectionWorker`
  (SPEC-12); the original "no Web API" constraint was dropped by the v2
  request.
