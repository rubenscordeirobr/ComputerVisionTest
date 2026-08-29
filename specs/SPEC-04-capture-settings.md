# SPEC-04 — Capture settings

## Objective

Editable capture configuration on `/capture-settings`: which object classes to
record, max segment duration, linger time (grace period), and confidence
threshold — persisted in the `CaptureSettings` singleton row.

## Scope

- Form page bound to the `CaptureSettings` singleton (loaded on init, saved on
  demand, PT-BR validation).
- Class selection sourced from `DetectableClasses` (SPEC-01), displayed with
  PT-BR labels while storing English COCO names.

## Out of scope

- The pipeline consuming these values — the console app still reads
  `appsettings.json` in v1. The page states this honestly (info alert).

## Dependencies

- SPEC-01 (entity, repository, `DetectableClasses`), SPEC-02 (page shell).

## Tasks

- [ ] `/capture-settings` page with a `MudForm` inside a `MudPaper`/card:
  - **Classes monitoradas**: `MudSelect` with `MultiSelection`, items from
    `DetectableClasses`, item text `"{ptBrLabel} ({name})"` (e.g.
    "pessoa (person)"), `MultiSelectionTextFunc` listing PT-BR labels; at
    least one class required.
  - **Duração máxima do segmento (s)**: `MudNumericField<int>`, range
    5–3600.
  - **Tempo de espera após o objeto sair (s)** (linger): 
    `MudNumericField<double>`, range 0–300, step 0.5.
  - **Confiança mínima**: `MudSlider<double>` 0.05–0.95 step 0.05 with the
    current value shown as percentage (or numeric field — pick one, show %).
- [ ] Load via `ISettingsRepository.GetCaptureSettingsAsync` on init; button
      **Salvar** → validate → `SaveCaptureSettingsAsync` → Snackbar
      "Configurações salvas." (errors → error snackbar).
- [ ] `MudAlert` (severity Info, PT-BR): the detection pipeline still reads
      `appsettings.json`; these values will drive it in a future version.

## Acceptance criteria

- Values load with the seeded defaults (person / 60 / 2,0 / 0,5) on a fresh
  database.
- Edited values survive an app restart (persisted in SQLite).
- Validation blocks: empty class list, out-of-range numbers; PT-BR messages.
- All labels/messages PT-BR; build green.

## Changelog

- 2026-08-29 — **Superseded by SPEC-10**: the single settings form was
  replaced by the capture-rules list (multiple rules, each with its own
  classes, thresholds and alert channels). This spec remains as history of
  the v1 behavior; the singleton's values were migrated into the first rule.
