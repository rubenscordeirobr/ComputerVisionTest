# SPEC-21 — Annotation colors per class, annotation limited to rule classes

Extends SPEC-10/12. Each capture rule may set the color used to draw a class on the
annotated stream and in the recorded clips, and the DetectionWorker only annotates
the classes that have a capture rule.

## Problem

The worker drew every object the model detected (80 COCO classes), each in a fixed
palette color chosen by class id. Users wanted a clean stream that only highlights
what they configured, in colors of their choosing (e.g. people in red, cars in blue).

## Design

- **Where the color is set**: on the capture rule, next to "Classes monitoradas".
  The rule already owns the class list, so a separate page would only duplicate the
  class selection. Once a class is selected, a "Cores na imagem" block lists each
  selected class with a native color input and a "Usar padrão" link. No color ⇒
  the worker's default palette color for that class.
- **Storage**: `CaptureRule.ClassColors` — `Dictionary<string,string>` (COCO name →
  `"#RRGGBB"`), JSON column `ClassColors` (migration `RuleClassColors`, default `{}`).
  `AnnotationColor.TryNormalize` / `Sanitize` (Core) accept `#abc`, `abc`, `#AABBCC`
  in any case and drop malformed entries; entries for unselected classes are removed on save.
- **API**: `WorkerRuleDto.ClassColors` rides with the rule (`GET /api/processor/capture-rules`).
- **Worker**:
  - `RecordingConfig.ClassColors` (also readable from `appsettings.json`
    `recording.classColors` for the offline fallback) is filled from the rules —
    rules are tenant-independent on the worker, so the **first enabled rule** that
    sets a color for a class wins.
  - `RecordingConfig.IsTracked(class)` — true when any rule (or the fallback
    `trackClasses`) lists the class, time windows ignored. `CameraPipeline` filters
    detections with it *before* tracking, so untracked classes get neither boxes nor
    track ids; recording still applies the per-rule thresholds and time windows.
  - `Annotator` is now an instance created once at startup with the color map;
    `ColorFor(track)` = custom color or `Palette[classId % 20]`, cached per class id.
- **Rules page**: each class chip shows a colored dot when the rule sets a color.

## Non-goals

- A per-tenant color catalog: the worker is tenant-agnostic, so a color is a property
  of the rule, not of the tenant.
- Live reload: as with the other rule fields, the worker reads rules at startup.
