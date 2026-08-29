# SPEC-02 — Web skeleton: Blazor Server + MudBlazor layout + navigation

## Objective

A running MudBlazor app shell in PT-BR: global InteractiveServer rendering,
side navigation to all six areas (placeholder pages where features are not yet
built), a minimal dashboard fed by the repositories, and README instructions
for running the web app.

## Scope

- MudBlazor installed and configured (services, providers, CSS/JS, fonts).
- `MainLayout` with `MudAppBar` + `MudDrawer`/`MudNavMenu`.
- Routed pages (English routes, PT-BR titles/labels):
  - `/` — **Painel** (dashboard)
  - `/cameras` — **Câmeras**
  - `/captures` — **Capturas**
  - `/capture-settings` — **Configurações de Captura**
  - `/alerts` — **Alertas**
  - `/system-settings` — **Sistema**
- Dashboard v1: summary cards — total/active cameras, total captures,
  captures today — each linking to its page.
- pt-BR culture as the app default (date/number formatting).
- Fixed dev URL (`http://localhost:5210`) in `launchSettings.json`.
- README section: how to run the management web app.

## Out of scope

- Any feature page content beyond placeholders (SPEC-03..07).
- Authentication, theming beyond the MudBlazor default.

## Dependencies

- SPEC-00 (Web project exists), SPEC-01 (repositories for the dashboard
  counts).

## Tasks

- [ ] Add `MudBlazor` (latest stable) to `Directory.Packages.props` and
      reference it from `Web`.
- [ ] `Program.cs`: `AddMudServices()`; set
      `CultureInfo.DefaultThreadCurrentCulture/UICulture = pt-BR`.
- [ ] `App.razor`: MudBlazor stylesheet + `MudBlazor.min.js` + Roboto font
      links; apply `InteractiveServer` render mode globally (on `Routes` and
      `HeadOutlet`).
- [ ] `_Imports.razor`: add `@using MudBlazor`.
- [ ] `MainLayout.razor`: `MudThemeProvider`, `MudPopoverProvider`,
      `MudDialogProvider`, `MudSnackbarProvider`; `MudLayout` with app bar
      (title **CameraVision**, drawer toggle) and drawer nav — PT-BR labels +
      Material icons per item (Painel, Câmeras, Capturas, Configurações de
      Captura, Alertas, Sistema).
- [ ] Create the six pages; non-dashboard pages show their PT-BR title and an
      "Em construção" placeholder (replaced by later specs).
- [ ] Dashboard: inject `ICameraRepository`/`ICaptureRepository`; cards with
      counts (câmeras ativas/total, capturas no total, capturas hoje) and
      navigation links.
- [ ] `launchSettings.json`: `applicationUrl = http://localhost:5210`.
- [ ] README: add **Management web app** section (`dotnet run --project
      src/CameraVision.Web` → http://localhost:5210; database auto-created at
      `data/database.db`).

## Acceptance criteria

- App starts at http://localhost:5210 with the MudBlazor layout; no browser
  console errors from missing MudBlazor assets.
- All six nav items navigate to their routes; every visible string is PT-BR.
- Dashboard shows real counts from the database (0s on a fresh db).
- Dates render in pt-BR format anywhere shown.
- Build green; console app unaffected.

## Changelog

- 2026-08-29 — Auth refactor note: SPEC-08 later extends this shell — a
  **Usuários** nav item (admin-only), a user menu with **Sair** (logout) in
  the app bar, and a login redirect in front of every page. Nothing changes
  in this spec's own tasks; once SPEC-08 lands, this spec's acceptance
  criteria are verified after logging in.
