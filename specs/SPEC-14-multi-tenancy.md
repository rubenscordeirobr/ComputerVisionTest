# SPEC-14 — Multi-tenancy (empresas) and role separation

## Objective

Turn the single-tenant management app into a multi-tenant one: every camera,
capture rule, capture, health event and alert recipient list belongs to a
**tenant** ("Empresa" in the UI). Users get a role — **User** (viewer),
**Admin** (tenant administrator) or **SuperAdmin** (platform operator). The
SuperAdmin manages tenants (create, edit, activate/deactivate) and owns the
system-wide configuration (SMTP, WhatsApp/Evolution, public URL, health-alert
tuning, capture-alert anti-flood); tenant admins manage only their own
tenant's data and users.

First tenant seeded from the current installation:

- Tenant **"Rubens Cordeiro"** — receives **all data existing at migration
  time** (cameras, capture rules, captures, health events, alert recipients).
- Tenant admin user **`rubens.cordeiro@live.com.br`** / `test` (hashed).
- The existing **`admin`** / `admin2026` account becomes **SuperAdmin**
  (no tenant).

## Scope

- `Tenant` entity + `Tenants` table; `TenantId` on `AppUser` (nullable — null
  = system user), `Camera`, `CaptureRule`, `Capture`, `CameraHealthEvent` and
  `AlertSettings` (all non-null).
- `UserRole` enum (`User`, `Admin`, `SuperAdmin`) replacing `AppUser.IsAdmin`.
- EF migration `MultiTenancy` with in-place data backfill (existing rows →
  tenant 1) and role mapping (`IsAdmin` → `Admin`, `admin` account →
  `SuperAdmin`).
- `DbInitializer` seeding: default tenant, SuperAdmin `admin`, tenant admin
  `rubens.cordeiro@live.com.br`, per-tenant default rule + alert settings.
- Tenant-aware repositories (explicit `tenantId` filter parameters) and a new
  `ITenantRepository`.
- Auth: `role` + `tenant_id` claims; policies `Admin` (Admin or SuperAdmin)
  and `SuperAdmin`; login blocked when the user's tenant is deactivated.
- Web refactor:
  - **/tenants** ("Empresas") — SuperAdmin-only CRUD (create, edit, activate/
    deactivate; no delete).
  - **/system-settings** ("Sistema") — SuperAdmin-only.
  - **/users** — tenant admins see/manage only their tenant; SuperAdmin sees
    all users, picks tenant + role (only SuperAdmin can grant SuperAdmin).
  - **/cameras**, **/captures**, **/capture-settings**, **/alerts**, **/**
    (dashboard) — filtered by the signed-in user's tenant; SuperAdmin sees
    everything (with an "Empresa" column / selector where relevant).
  - Health-alert tuning tab and capture anti-flood section — SuperAdmin-only
    (system-level settings).
- Alert pipeline routing per tenant: capture alerts and camera-health alerts
  are matched against the *capture's/camera's tenant* rules and delivered to
  that tenant's recipients; digests are built per tenant.
- Media authorization: `/media` (API) and the authorized playback page only
  serve captures of the signed-in user's tenant (SuperAdmin: all). Tokenized
  public links keep working per capture.

## Out of scope

- Deleting tenants (would cascade into footage; deactivation covers the need).
- Per-tenant recording on the DetectionWorker: the worker keeps applying the
  **union of all tenants' enabled rules** to every camera it processes.
  Correctness is preserved at dispatch time (a capture only alerts if *its own
  tenant* has a matching rule), at the cost of possibly recording clips no
  rule of that tenant asked for. Per-tenant worker rules are a future spec.
- Per-tenant SMTP servers, health-alert tuning or anti-flood windows — those
  stay system-wide (SuperAdmin); only *recipients* (AlertSettings) are
  per-tenant. Cooldown/flood-cap counters therefore remain global.
- Per-tenant camera-name namespaces: camera names stay **globally unique**
  because they map to shared output folders (`output/{date}/{camera}`).
- Tenant self-service sign-up, branding, quotas, billing.
- Forcing re-authentication: cookies issued before this change lack the new
  claims — those sessions must simply sign in again (documented behavior).

## Dependencies

- SPEC-01 (domain/data), SPEC-08 (auth + users), SPEC-11 (API/media),
  SPEC-13 (health alerts). Extends all of them.

## Design

### Tenant model and data scoping

`Tenant`: `Id`, `Name` (unique, max 100), `IsActive` (default true),
`CreatedAt`. UI term: **Empresa**.

Scoping is **explicit**: repository query methods take an `int? tenantId`
filter (`null` = no filter, used by SuperAdmin views and system services)
instead of EF global query filters. With `IDbContextFactory` + singleton
repositories shared by two processes and several background services, an
ambient-tenant provider would be fragile; explicit parameters keep every call
site auditable. `CaptureFilter` gains a `TenantId` for the captures grid.

`Capture.TenantId` is denormalized (copied from the camera at ingest/index
time) so footage stays owned after a camera is deleted (`CameraId` is
nullable). Same for `CameraHealthEvent.TenantId`.

The **default tenant** (lowest `Id`, i.e. the seeded one) absorbs data that
arrives without a resolvable tenant: cameras auto-created by the capture
import and ingested captures whose camera is unknown.

### Roles and claims

`UserRole` (stored as string): `User` (viewer), `Admin` (manages own tenant's
cameras/rules/recipients/users), `SuperAdmin` (`TenantId = null`; manages
tenants, all users and system settings). Claims issued at login: existing
ones plus `role` and `tenant_id` (omitted for system users). Policies:
`Admin` → role ∈ {Admin, SuperAdmin}; `SuperAdmin` → role = SuperAdmin.
Shared claim helpers live in `CameraVision.Core.Auth.AppClaims` (used by both
Web pages and the API media guard). Login additionally rejects users of a
deactivated tenant ("Empresa desativada.").

### System vs tenant settings

| Setting | Level | Editor |
| --- | --- | --- |
| `SystemSettings` (SMTP, Evolution, public URL) | system singleton | SuperAdmin |
| `HealthAlertSettings` (thresholds, cooldown, flood cap, digest) | system singleton | SuperAdmin |
| `CaptureAlertSettings` (grouping window) | system singleton | SuperAdmin |
| `AlertSettings` (channel on/off + recipients) | **per tenant** (unique TenantId+Channel) | tenant Admin (SuperAdmin via tenant selector) |
| `CaptureRule` | **per tenant** | tenant Admin |

### Alert routing per tenant

- `AlertDispatcher` groups fresh captures by `TenantId`; each group is matched
  against **that tenant's** enabled rules and sent to **that tenant's**
  recipients. The grouping switch/window stays global.
- `CaptureAlertDigestHostedService` groups pending captures by tenant and
  sends one summary per tenant per window.
- `CameraHealthAlertService` stamps events with the camera's tenant and sends
  individual health alerts to that tenant's recipients;
  `HealthDigestHostedService` builds one digest per tenant. Debounce/cooldown/
  flood-cap logic (system-wide) is unchanged.

### Media authorization

The API `/media` guard, after cookie auth, resolves the requested file
(`.mp4`, or its `.jpg` thumbnail) to a capture and requires
`capture.TenantId == tenant_id` claim unless the user is SuperAdmin. Unknown
files are denied for tenant users. Tokenized links (`?token=`) are unchanged
— a token only ever matches one capture. The authorized playback page
(`/captures/{id}/play`) applies the same ownership check.

### Migration and seeding

Migration `MultiTenancy` (schema + deterministic backfill, raw SQL inside
`Up()`):

1. Create `Tenants`; insert tenant `1` = "Rubens Cordeiro" **only when the
   database already has users** (i.e. an existing installation — fresh
   databases are seeded by `DbInitializer` instead).
2. Add `TenantId` columns with default `1` (backfills every existing row);
   `Users.TenantId` nullable; add `Users.Role` (default `User`).
3. `Role = 'Admin'` where `IsAdmin = 1`; then `admin` → `SuperAdmin`,
   `TenantId = NULL`; drop `IsAdmin`.
4. AlertSettings unique index becomes (`TenantId`, `Channel`).

`DbInitializer` (idempotent, runs in Web + API):

- Ensure at least one tenant ("Rubens Cordeiro").
- Seed `admin` / `admin2026` as SuperAdmin when no user exists.
- Seed `rubens.cordeiro@live.com.br` / `test` (Role Admin, default tenant)
  when that username is missing.
- Default capture rule and per-channel `AlertSettings` seeded for the default
  tenant.

## Tasks

- [ ] Core: `Tenant`, `UserRole`, `TenantId` fields, `ITenantRepository`
      (+ per-tenant summaries with user/camera counts), tenant filter
      parameters on repositories, `CaptureFilter.TenantId`,
      `Auth/AppClaims` helpers.
- [ ] Infrastructure: model config + `MultiTenancy` migration with backfill;
      `TenantRepository`; tenant-aware repository implementations;
      `SettingsRepository.Get/SaveAlertSettingsAsync(tenantId, channel)`;
      `DbInitializer` seeding; `CaptureIndexer` tenant stamping;
      `AlertDispatcher` per-tenant matching/delivery.
- [ ] API: ingest stamps `TenantId` from the camera (default tenant as
      fallback); `/media` tenant ownership check.
- [ ] Web: `SuperAdmin` policy + `role`/`tenant_id` claims at login (+ tenant
      active check); nav gains **Empresas** and hides **Sistema** for
      non-SuperAdmin; `/tenants` page + dialog; `/users` tenant/role aware;
      `/system-settings` SuperAdmin-only; tenant filtering on dashboard,
      cameras, captures, rules, playback; per-tenant recipients on `/alerts`
      (tenant selector for SuperAdmin, health tab SuperAdmin-only); anti-flood
      section SuperAdmin-only; health/capture digest services per tenant.
- [ ] Docs: README (roles, seeded credentials), CLAUDE.md architecture note.

## Acceptance criteria

- Migration on the existing database: every pre-existing camera, rule,
  capture, health event and recipient list belongs to tenant "Rubens
  Cordeiro"; `admin` signs in as SuperAdmin; `rubens.cordeiro@live.com.br` /
  `test` signs in as tenant admin of that tenant.
- Tenant admin sees only their tenant's data on every page, manages their
  users (cannot grant SuperAdmin, cannot self-demote/self-deactivate), edits
  their recipients and rules, and **cannot** open `/tenants` or
  `/system-settings` (nav hidden + direct URL → acesso negado).
- SuperAdmin creates/edits/deactivates tenants; users of a deactivated tenant
  cannot sign in ("Empresa desativada."); SuperAdmin edits system settings
  and sees all tenants' data (with tenant column/selector).
- A capture only triggers alerts for rules and recipients **of its own
  tenant**; health alerts and both digests go to the camera's tenant
  recipients only.
- Tenant users cannot stream or play another tenant's capture (direct
  `/media` URL or playback page → denied); tokenized e-mail links still play.
- Fresh database: seeding produces the same tenant/users; build green;
  DetectionWorker keeps working unchanged against the API.
