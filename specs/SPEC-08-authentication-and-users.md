# SPEC-08 — Authentication + user management

## Objective

Cookie-based authentication in front of the whole web app (login page, logout,
seeded `admin` user with a hashed password) plus an admin-only user management
screen (list, create, edit, deactivate, reset password).

## Scope

- Cookie authentication + authorization wiring, login/logout, redirect of
  unauthenticated visitors to `/login` (preserving `returnUrl`).
- Auth guard on the `/media` static files (recordings are sensitive footage).
- Seeding `admin` / `admin2026` (hashed) on first run.
- `/users` management screen, visible and accessible only to admin users.
- Shell updates: **Usuários** nav item (admin-only) and an app-bar user menu
  with **Sair**.

## Out of scope

- Full ASP.NET Core Identity (stores, managers, 2FA, lockout, scaffolded UI).
- Roles/permissions beyond the single `IsAdmin` flag.
- Force-terminating active circuits when a user is deactivated — v1 blocks
  the user at their next login; an already-open session survives until its
  cookie expires (documented limitation).
- Password self-service/change-own-password screen (admin resets passwords).

## Dependencies

- SPEC-01 (`AppUser` + `IUserRepository` exist in the initial migration),
  SPEC-02 (shell), SPEC-05/07 (pages being protected; `/media` mapping).

## Auth design (and why not full Identity)

**Chosen**: plain cookie authentication + the standalone
`PasswordHasher<AppUser>` from `Microsoft.Extensions.Identity.Core`, over a
custom `AppUser` table accessed through the existing repository pattern.

Justification: full ASP.NET Core Identity would bring `UserManager`/
`SignInManager`, its own EF entity model (7+ tables), and Razor-Pages-flavored
scaffolding — all to serve one seeded admin and a handful of LAN users with no
roles, 2FA, or external logins. The lean scheme reuses the repositories the
app already has, keeps the schema to one table, and still stores passwords in
the vetted Identity hash format (PBKDF2, versioned) via `PasswordHasher`, so a
future migration to full Identity stays possible.

Blazor Server detail: an interactive circuit cannot issue auth cookies
(`HttpContext.SignInAsync` needs a real HTTP request). Therefore the login
page is rendered in **static SSR** (`[ExcludeFromInteractiveRouting]`; the
`App.razor` render mode becomes conditional via
`HttpContext.AcceptsInteractiveRouting()`), posts a normal form, signs in
during the request, and redirects. Logout is a small `POST /logout` endpoint
(antiforgery-validated, auth plumbing — not a data API) triggered by a native
form in the app bar.

Claims issued at login: `NameIdentifier` (user id), `Name` (username),
`display_name`, `is_admin`. Authorization policy `Admin` requires
`is_admin=true`. Cookie: `LoginPath=/login`, sliding expiration 12 h.

## Tasks

- [ ] Add `Microsoft.Extensions.Identity.Core` to CPM; register
      `IPasswordHasher<AppUser>` (singleton).
- [ ] `Program.cs`: `AddAuthentication().AddCookie(...)`,
      `AddAuthorization` with the `Admin` policy,
      `AddCascadingAuthenticationState()`; `UseAuthentication` /
      `UseAuthorization` in the pipeline (before the `/media` static files).
- [ ] `DbInitializer`: seed `admin` / `admin2026` (hashed, `IsAdmin=true`,
      `IsActive=true`) when no user exists.
- [ ] `App.razor`: conditional per-page render mode
      (`AcceptsInteractiveRouting()` → InteractiveServer, else static SSR).
- [ ] `Login.razor` (`/login`, `[AllowAnonymous]`,
      `[ExcludeFromInteractiveRouting]`): static SSR form (username +
      password + antiforgery), verifies user exists, `IsActive`, and password
      hash; on success `SignInAsync` + redirect to local `returnUrl` (default
      `/`); on failure PT-BR error ("Usuário ou senha inválidos.", "Usuário
      desativado."). PT-BR labels throughout.
- [ ] Logout: `POST /logout` endpoint (validates antiforgery, `SignOutAsync`,
      redirect to `/login`); app bar gets a user menu showing the display
      name/username with item **Sair** submitting that form.
- [ ] Require auth everywhere: `@attribute [Authorize]` via
      `Components/Pages/_Imports.razor`; `Routes.razor` uses
      `AuthorizeRouteView` + a `RedirectToLogin` component (full-load
      navigate to `/login?returnUrl=...`).
- [ ] `/media` guard: unauthenticated requests get 401 (middleware before the
      output-root static files).
- [ ] `/users` page (`[Authorize(Policy="Admin")]`): `MudTable` (Usuário,
      Nome, Administrador, Ativo, Criado em) + toolbar **Novo usuário**;
      dialogs:
      - create: username (required, unique), display name, password
        (required, min 6) + confirmation, admin/active switches;
      - edit: display name, admin/active (no password fields);
      - reset password: new password + confirmation.
      Guards: the logged-in admin cannot deactivate itself nor remove its own
      admin flag (PT-BR message).
- [ ] Nav: **Usuários** item wrapped in `AuthorizeView Policy="Admin"`.
- [ ] README: document the seeded credentials (`admin` / `admin2026`) and
      recommend resetting the password after first login.

## Acceptance criteria

- Anonymous visits to any page (including deep links) land on `/login` and
  return to the original URL after signing in; `/media/...` returns 401 when
  unauthenticated.
- `admin` / `admin2026` logs in on a fresh database; the password is stored
  hashed (no plaintext anywhere in the DB).
- Wrong password and deactivated users are rejected with PT-BR messages.
- **Sair** signs out and returns to the login page; the back button does not
  reveal protected content after logout (new requests redirect).
- Admin sees **Usuários**; a non-admin user neither sees the nav item nor can
  open `/users` directly.
- Creating, editing, deactivating, and resetting the password of users works
  and survives restarts; a deactivated user can no longer log in;
  self-deactivation/self-demotion is blocked.
- Build green; all prior specs' pages still work (behind login).
