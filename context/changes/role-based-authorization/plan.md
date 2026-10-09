# Role-Based Authorization & Seeded Administrator Account Implementation Plan

## Overview

Make the two roles defined in the PRD (Administrator, Teacher) real and enforced across the application instead of merely seeded as empty `IdentityRole` rows. This change locks every endpoint down by default, seeds an actual administrator **user** account from configuration, closes the public self-registration hole left open by `AddDefaultUI()`, adds a minimal role-scoped landing page per role so the boundary is visibly demonstrable, and backs it all with an automated test project that future slices (S-01..S-05) can extend for their own authorization checks.

This is roadmap item **F-01** — a foundation slice that unlocks S-01, S-02, S-03, and S-05.

## Current State Analysis

The repo is further along than a bare MVC scaffold: ASP.NET Core Identity and EF Core are already wired up, but role *enforcement* has not started.

- `src/melody/Program.cs:9-24` already registers `ApplicationDbContext` (SQL Server), `AddIdentity<ApplicationUser, IdentityRole>()` with `AddDefaultUI()`, and configures `LoginPath`/`AccessDeniedPath` — but **no fallback/global authorization policy** is configured, so every endpoint is anonymous-accessible by default.
- `src/melody/Program.cs:54` calls `RoleSeeder.SeedAsync(scope.ServiceProvider)` at startup, which only creates the `Administrator` and `Teacher` `IdentityRole` rows (`src/melody/Data/RoleSeeder.cs`) — **no administrator user account exists**, so nobody can actually log in as Administrator today.
- `src/melody/Models/Domain/ApplicationUser.cs` extends `IdentityUser` with `DisplayName` and a nullable `Teacher` navigation (null ⇒ Administrator).
- `src/melody/Controllers/HomeController.cs` has **no `[Authorize]`** — `Index`/`Privacy`/`Error` are all public today.
- `AddDefaultUI()` pulls in the Identity Razor Class Library's scaffolded pages (`/Identity/Account/Register`, `/Login`, etc.) — none of them are scaffolded into the project (`glob src/melody/Areas/**` → no matches), so they come from the RCL as-is, including a **public self-registration page**, which contradicts the PRD's Access Control section ("no public access without login... Uczniowie nie mają własnego konta"; accounts are created by an Administrator per FR-003/FR-004).
- `src/melody/Views/Shared/_LoginPartial.cshtml:18-21` shows a "Register" link to every anonymous visitor.
- No test project exists in `melodybooker.sln` yet (confirmed via AGENTS.md and `glob`).
- The solution builds cleanly today (`dotnet build melodybooker.sln` → 0 errors).

### Key Discoveries:

- `AllowAnonymous` metadata on Identity's RCL-provided `Register` page **overrides any fallback/explicit authorization policy** — a `RazorPagesOptions.Conventions.AuthorizeAreaPage(...)` convention alone cannot lock it down (confirmed via research: ASP.NET Core's `AllowAnonymous` short-circuits authorization for an endpoint regardless of any other authorize metadata present). The only reliable fix is to scaffold just that one page into the project and replace its `[AllowAnonymous]` with `[Authorize(Roles = "Administrator")]`.
- `ConfigureApplicationCookie` in `Program.cs:26-30` already sets `LoginPath`/`AccessDeniedPath` to the Identity RCL's existing pages, so a fallback-deny policy "just works" for the redirect UX — no new login/access-denied pages need to be built in this change.
- `RoleSeeder.SeedAsync` runs *before* `app.Run()` inside the existing `using (var scope = ...)` block (`Program.cs:49-56`) — the new admin-user seeder must run **after** `RoleSeeder.SeedAsync` so the `Administrator` role already exists when the seeder assigns it.

## Desired End State

- Every HTTP endpoint requires an authenticated user by default; the only reachable anonymous surface is Identity's own Login/ForgotPassword/etc. pages (via their existing `AllowAnonymous` metadata) and static assets/health checks.
- On a fresh environment (local dev or a new deploy), starting the app with `Admin:Email`/`Admin:Password` configured creates exactly one Administrator-role user if none exists yet; running it again is a no-op (idempotent).
- Visiting `/Identity/Account/Register` as an anonymous user, or as a signed-in Teacher, redirects to Access Denied / Login; only a signed-in Administrator can reach it.
- Logging in as the seeded Administrator lands on an Administrator-only page (`/Admin`); logging in as a Teacher (seeded manually for testing, since teacher creation is out of scope — see below) lands on a Teacher-only page (`/Teacher`); each role is blocked (403) from the other's page.
- `dotnet test` runs a new test project that asserts: anonymous → redirect-to-login, wrong-role → 403, correct-role → 200, and that the admin seeder is idempotent.

**Verification**: `dotnet build melodybooker.sln` succeeds, `dotnet test` passes, and manual login as Administrator vs. a role-seeded Teacher test user shows the role-appropriate landing page and a 403 on the other role's page.

## What We're NOT Doing

- **Not** building the admin UI to create/edit teachers and students (FR-003/FR-004/FR-005/FR-006) — that's S-01 (`admin-roster-management`), which depends on this change.
- **Not** building the teacher's real student list or booking screens (FR-007/FR-008..FR-011) — that's S-02/S-03/S-04.
- **Not** building admin reservation oversight (FR-013/FR-014) — that's S-05.
- **Not** hard-blocking specialist-room mismatches (FR-012) — explicitly nice-to-have, out of MVP scope per repo instructions.
- **Not** adding email confirmation, 2FA, or a custom password policy beyond ASP.NET Core Identity's defaults — `RequireConfirmedAccount = false` is already set and is out of scope to revisit here.
- **Not** adding deployment-slot/CI wiring (that's F-02, `deployment-skeleton`, independently blocked on Azure provisioning).
- **Not** scaffolding or customizing any other Identity RCL page (Login, ForgotPassword, Manage/*) beyond the single `Register` page that must be locked down.

## Implementation Approach

Lock down first, then grant back exactly what's needed: configure a fallback-deny authorization policy, seed the one real account the system needs today (Administrator), close the self-registration hole, and give each role a concrete (if minimal) landing page so the enforcement is demonstrable in a browser — not just in code review. Finish with an integration test project so every later slice inherits a working pattern for asserting its own authorization rules, per the "verified, not just scaffolded" outcome F-01 is meant to unlock.

## Critical Implementation Details

- **`AllowAnonymous` beats everything else.** The Identity RCL's `Register` page ships with `AllowAnonymous` applied via an internal page-route convention, not a per-page attribute you can override with `services.AddRazorPages(options => ...)` conventions alone. The only working fix is `dotnet aspnet-codegenerator identity -dc ApplicationDbContext --files "Account.Register"` to pull `Register.cshtml`/`.cs` into `Areas/Identity/Pages/Account/` in the project, then edit the scaffolded `RegisterModel` directly to remove `[AllowAnonymous]` and add `[Authorize(Roles = "Administrator")]`.
- **Seeding order matters.** The new admin-user seeder must be invoked *after* `RoleSeeder.SeedAsync(scope.ServiceProvider)` in `Program.cs`'s existing startup scope, since it looks up the `Administrator` `IdentityRole` by name and will fail (or silently no-op, depending on `UserManager.AddToRoleAsync` behavior) if the role doesn't exist yet.

## Phase 1: Global authorization policy

### Overview

Require authentication on every endpoint by default, relying on Identity's existing `AllowAnonymous` metadata on its own Login/ForgotPassword/etc. pages to keep those reachable, and bring `HomeController` under the same policy.

### Changes Required:

#### 1. Fallback authorization policy

**File**: `src/melody/Program.cs`

**Intent**: Configure authorization so every endpoint requires an authenticated user unless it explicitly opts out, matching the PRD's Access Control requirement that unauthenticated users are always redirected to login.

**Contract**: Add an `AddAuthorization` call configuring `options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();`, registered before `builder.Build()`. Must be added alongside (not replacing) the existing `AddIdentity`/`ConfigureApplicationCookie` calls.

#### 2. HomeController requires authentication

**File**: `src/melody/Controllers/HomeController.cs`

**Intent**: `HomeController` has no explicit authorization today and would otherwise rely solely on the new fallback policy; make the requirement explicit on the controller so it's self-documenting for future readers.

**Contract**: Add `[Authorize]` at the class level (`using Microsoft.AspNetCore.Authorization;`). No role restriction — any authenticated user (Administrator or Teacher) may reach `Home/Index`, `Privacy`, and `Error`.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build melodybooker.sln`
- New integration test (added in Phase 5) asserts an anonymous `GET /` redirects to the login path

#### Manual Verification:

- Visiting `/` while logged out redirects to `/Identity/Account/Login`
- Visiting `/Identity/Account/Login` while logged out still loads (not redirected)

---

## Phase 2: Seeded administrator account

### Overview

Add an idempotent startup seeder that creates one Administrator-role user from configuration, so an admin can log in on a fresh environment without any public sign-up flow.

### Changes Required:

#### 1. Admin credentials configuration

**File**: `src/melody/appsettings.json`, `src/melody/appsettings.Development.json`

**Intent**: Define the configuration shape the seeder reads (`Admin:Email`, `Admin:Password`), following the same `ConnectionStrings` pattern already used for the dev-only `DefaultConnection` value. Production values come from Azure App Service Application Settings (env vars `Admin__Email` / `Admin__Password`, per ASP.NET Core's configuration key-section-to-env-var convention already assumed by `infrastructure.md`'s Secrets section); local dev values come from `dotnet user-secrets` (not committed).

**Contract**: `appsettings.json` gets an empty `"Admin": { "Email": "", "Password": "" }` section (placeholder, never a real credential, matching how `DefaultConnection` is already left blank in the committed base file). `appsettings.Development.json` is **not** given a real password either — document in a code comment / README note that local dev must run `dotnet user-secrets set "Admin:Password" "<value>"` from `src/melody/`.

#### 2. AdminUserSeeder

**File**: `src/melody/Data/AdminUserSeeder.cs` (new)

**Intent**: Mirror `RoleSeeder`'s static-seeder pattern to create exactly one Administrator user if `Admin:Email` resolves to no existing user, assigning the `Administrator` role. Must be safe to run on every startup (idempotent) and must not throw/crash the app if `Admin:Email`/`Admin:Password` are unset — in that case it should log a warning and skip (so a fresh clone without secrets configured still boots for non-admin-dependent local work), rather than crashing.

**Contract**: `public static class AdminUserSeeder { public static async Task SeedAsync(IServiceProvider services, IConfiguration config) }`. Uses `UserManager<ApplicationUser>.FindByEmailAsync` to check existence, `CreateAsync(user, password)` to create, and `AddToRoleAsync(user, "Administrator")` to assign the role. Sets `EmailConfirmed = true` on the created user (no email-confirmation flow exists, matching `RequireConfirmedAccount = false`).

#### 3. Wire seeder into startup

**File**: `src/melody/Program.cs`

**Intent**: Invoke `AdminUserSeeder.SeedAsync` in the existing startup scope, immediately after `RoleSeeder.SeedAsync` so the `Administrator` role already exists (see Critical Implementation Details).

**Contract**: One additional `await AdminUserSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);` line after the existing `await RoleSeeder.SeedAsync(scope.ServiceProvider);` call.

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build melodybooker.sln`
- New unit test asserts calling `AdminUserSeeder.SeedAsync` twice results in exactly one Administrator user (idempotency)
- New unit test asserts the seeder does not throw when `Admin:Email`/`Admin:Password` are unset

#### Manual Verification:

- With `Admin:Email`/`Admin:Password` set via user-secrets, running the app locally and logging in with those credentials succeeds and the account is recognized as Administrator

---

## Phase 3: Lock down public self-registration

### Overview

Close the public self-registration hole by scaffolding only the Identity `Register` page into the project and restricting it to the Administrator role; adjust the nav so anonymous visitors never see a "Register" link.

### Changes Required:

#### 1. Scaffold and restrict the Register page

**File**: `src/melody/Areas/Identity/Pages/Account/Register.cshtml`, `Register.cshtml.cs` (new, via scaffolding)

**Intent**: Pull only this one page out of the Identity RCL into the project so its `[AllowAnonymous]` metadata can be removed — a fallback policy or routing convention alone cannot override RCL-applied `AllowAnonymous` (see Critical Implementation Details).

**Contract**: Run `dotnet aspnet-codegenerator identity -dc melody.Data.ApplicationDbContext --files "Account.Register"` from `src/melody/` (requires the `dotnet-aspnet-codegenerator` global tool). In the generated `RegisterModel`, remove `[AllowAnonymous]` and add `[Authorize(Roles = "Administrator")]` at the class level.

#### 2. Hide Register link from anonymous visitors

**File**: `src/melody/Views/Shared/_LoginPartial.cshtml`

**Intent**: The current markup shows "Register" to every signed-out visitor; since registration now requires an existing Administrator session, showing it to anonymous users is misleading (clicking it just bounces to Login/Access Denied).

**Contract**: Move the "Register" `<li>` into the `SignInManager.IsSignedIn(User)` branch, gated additionally on `User.IsInRole("Administrator")` (use `@if (User.IsInRole("Administrator"))` inside that branch), so only a logged-in Administrator sees the link; the anonymous (`else`) branch keeps only "Login".

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build melodybooker.sln`
- New integration test asserts anonymous `GET /Identity/Account/Register` does not return 200 (redirects to login)
- New integration test asserts a Teacher-role authenticated `GET /Identity/Account/Register` returns 403

#### Manual Verification:

- Logged out, the nav shows only "Login" (no "Register" link)
- Logged in as Administrator, the nav shows "Register"; visiting it loads the registration form
- Logged in as Teacher, the nav does not show "Register"; visiting the URL directly shows Access Denied

---

## Phase 4: Role-scoped landing stubs

### Overview

Give each role a minimal, role-gated landing page so logging in as Administrator vs. Teacher visibly lands somewhere different — making the role boundary demonstrable in a browser today, ahead of S-01 (admin roster) and S-02 (teacher student list) building their real pages on top of these controllers.

### Changes Required:

#### 1. AdminController

**File**: `src/melody/Controllers/AdminController.cs` (new), `src/melody/Views/Admin/Index.cshtml` (new)

**Intent**: A single `Index` action gated to the Administrator role, with a placeholder view confirming the Administrator landing page is reachable. S-01 will extend this controller with the actual roster-management actions.

**Contract**: `[Authorize(Roles = "Administrator")] public class AdminController : Controller { public IActionResult Index() => View(); }`, route defaults to `/Admin`.

#### 2. TeacherController

**File**: `src/melody/Controllers/TeacherController.cs` (new), `src/melody/Views/Teacher/Index.cshtml` (new)

**Intent**: Same pattern as `AdminController`, gated to the Teacher role. S-02 will extend this with the real assigned-students list.

**Contract**: `[Authorize(Roles = "Teacher")] public class TeacherController : Controller { public IActionResult Index() => View(); }`, route defaults to `/Teacher`.

#### 3. Role-based post-login redirect

**File**: `src/melody/Views/Home/Index.cshtml`, `src/melody/Controllers/HomeController.cs`

**Intent**: Since `HomeController.Index` is now the default authenticated landing page for both roles, redirect each role to its own controller immediately so the "role-appropriate landing page" outcome is visible without a user having to navigate manually.

**Contract**: In `HomeController.Index`, add: if `User.IsInRole("Administrator")` → `RedirectToAction("Index", "Admin")`; else if `User.IsInRole("Teacher")` → `RedirectToAction("Index", "Teacher")`; otherwise fall through to the existing `View()` (defensive default — should not occur given only two roles exist).

### Success Criteria:

#### Automated Verification:

- Solution builds: `dotnet build melodybooker.sln`
- New integration test asserts an Administrator-role user `GET /Admin` returns 200 and `GET /Teacher` returns 403
- New integration test asserts a Teacher-role user `GET /Teacher` returns 200 and `GET /Admin` returns 403
- New integration test asserts logging in redirects each role to its own controller

#### Manual Verification:

- Logging in as the seeded Administrator account lands on `/Admin` with a visible placeholder page
- Logging in as a Teacher test account lands on `/Teacher` with a visible placeholder page
- Each role gets a 403/Access-Denied page when navigating directly to the other role's URL

---

## Phase 5: Automated verification

### Overview

Add the test project the repo doesn't have yet, covering the authorization behavior introduced in Phases 1-4, so S-01..S-05 inherit a working pattern for their own role-scoped tests.

### Changes Required:

#### 1. Test project

**File**: `tests/melody.Tests/melody.Tests.csproj` (new)

**Intent**: Stand up an xUnit + `WebApplicationFactory<Program>` integration test project targeting `net10.0`, referencing `src/melody/melody.csproj`, following the conventional `Microsoft.NET.Test.Sdk` + `xunit` + `xunit.runner.visualstudio` setup so `dotnet test`/`dotnet test --filter FullyQualifiedName~X.Y` work per repo conventions (AGENTS.md).

**Contract**: Package references: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.AspNetCore.Mvc.Testing` (version `10.0.12`, matching the main project's EF/Identity package versions), plus a `<ProjectReference Include="..\..\src\melody\melody.csproj" />`. Register the project in `melodybooker.sln` (new nested project entry under the existing `tests` or top-level solution folder).

#### 2. Test WebApplicationFactory with an in-memory/test database and role/user helpers

**File**: `tests/melody.Tests/MelodyWebApplicationFactory.cs` (new)

**Intent**: Override the `DefaultConnection` so tests don't require a real SQL Server instance, and expose a helper to sign in as a given role (Administrator/Teacher) for authenticated-request assertions.

**Contract**: `public class MelodyWebApplicationFactory : WebApplicationFactory<Program>` overriding `ConfigureWebHost` to replace `ApplicationDbContext`'s `DbContextOptions` with the EF Core SQLite in-memory or `UseInMemoryDatabase` provider, and to ensure roles + one Administrator + one Teacher test user exist via the existing seeders before tests run. Expose an `HttpClient` factory helper that authenticates as a given role (e.g., via a test-only authentication handler, since standing up real cookie-based login per test is heavier than needed here).

**Known gotcha** (confirmed during implementation): this project's migrations are raw SQL authored against the real SQL Server provider, so they can't run against the SQLite in-memory/`UseInMemoryDatabase` substitute. `WebApplicationFactory<Program>` + raw-SQL EF migrations commonly requires an environment-aware branch in the app's own startup (`Program.cs`) — e.g. `if (app.Environment.IsEnvironment("Testing")) await db.Database.EnsureCreatedAsync(); else await db.Database.MigrateAsync();` — rather than something containable entirely inside the test project. Plan for this branch up front instead of discovering it mid-implementation.

#### 3. Authorization integration tests

**File**: `tests/melody.Tests/AuthorizationTests.cs` (new)

**Intent**: Assert the concrete behaviors described in Phases 1, 3, and 4's Success Criteria in one place.

**Contract**: `[Fact]` tests for: anonymous `GET /` → redirect to login path; anonymous `GET /Identity/Account/Register` → not 200; Teacher `GET /Identity/Account/Register` → 403; Administrator `GET /Admin` → 200 and `GET /Teacher` → 403; Teacher `GET /Teacher` → 200 and `GET /Admin` → 403; Administrator `GET /` → redirects to `/Admin` and Teacher `GET /` → redirects to `/Teacher` (the post-login redirect promised by Phase 4's Success Criteria).

#### 4. Admin seeder unit tests

**File**: `tests/melody.Tests/AdminUserSeederTests.cs` (new)

**Intent**: Assert `AdminUserSeeder` is idempotent and safe when configuration is missing, per Phase 2's Success Criteria.

**Contract**: `[Fact]` tests calling `AdminUserSeeder.SeedAsync` twice against a fresh in-memory-provider `ApplicationDbContext`/`UserManager`, asserting exactly one Administrator user exists afterward; and a test calling it with empty `Admin:Email`/`Admin:Password` configuration asserting no exception is thrown and no user is created.

### Success Criteria:

#### Automated Verification:

- `dotnet test` passes with all new tests green
- `dotnet build melodybooker.sln` still succeeds with the new test project registered

#### Manual Verification:

- `dotnet test --filter FullyQualifiedName~AuthorizationTests` runs in isolation and passes
- `dotnet test --filter FullyQualifiedName~AdminUserSeederTests` runs in isolation and passes

---

## Testing Strategy

### Unit Tests:

- `AdminUserSeeder` idempotency (seed twice → one user)
- `AdminUserSeeder` graceful no-op when `Admin:Email`/`Admin:Password` are unset

### Integration Tests:

- Anonymous access to `/` and other authenticated routes redirects to login
- Anonymous and wrong-role access to `/Identity/Account/Register` is denied; Administrator access succeeds
- Role-scoped `/Admin` and `/Teacher` controllers enforce their respective role, 403 the other
- Post-login redirect sends each role to its own landing controller

### Manual Testing Steps:

1. Run the app locally with `Admin:Email`/`Admin:Password` set via `dotnet user-secrets`; confirm the admin account is created on first run and not duplicated on restart.
2. Log in as Administrator; confirm redirect to `/Admin`, and that `/Teacher` is denied.
3. Create a Teacher test user directly via `UserManager` (no UI exists yet — out of scope), log in, confirm redirect to `/Teacher` and that `/Admin` is denied.
4. Confirm the "Register" link is absent when logged out, visible only when logged in as Administrator.
5. Confirm visiting `/Identity/Account/Register` directly while logged out or as Teacher results in Access Denied / redirect to login, not the registration form.

## Performance Considerations

None — this change affects authorization middleware and a handful of near-empty controllers; no measurable performance impact expected for this MVP's scale (per PRD Non-Functional Requirements: no hard performance requirements).

## Migration Notes

No EF Core schema changes in this plan — `ApplicationUser`/`IdentityRole` tables already exist from the initial migration (`src/melody/Data/Migrations/20261005143616_InitialCreate.cs`). The admin user is created via `UserManager`, not a migration, so no new migration is required.

## References

- PRD: `context/foundation/prd.md` (FR-001, FR-002, Access Control, NFR on minors' data access)
- Roadmap: `context/foundation/roadmap.md` (F-01: role-based-authorization)
- Tech stack: `context/foundation/tech-stack.md` (.NET/ASP.NET Core, SQL Server, Azure App Service)
- Infrastructure: `context/foundation/infrastructure.md` (Secrets section — Application Settings pattern for `Admin:Email`/`Admin:Password` in production)
- Current startup wiring: `src/melody/Program.cs:1-61`
- Existing role seeder pattern to mirror: `src/melody/Data/RoleSeeder.cs`
- Existing login nav: `src/melody/Views/Shared/_LoginPartial.cshtml`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Global authorization policy

#### Automated

- [x] 1.1 Solution builds: `dotnet build melodybooker.sln` — 8f3ac3a
- [x] 1.2 Integration test: anonymous `GET /` redirects to login path — b4513d3

#### Manual

- [x] 1.3 Visiting `/` while logged out redirects to `/Identity/Account/Login` — 8f3ac3a
- [x] 1.4 Visiting `/Identity/Account/Login` while logged out still loads — 8f3ac3a

### Phase 2: Seeded administrator account

#### Automated

- [x] 2.1 Solution builds: `dotnet build melodybooker.sln` — 00f03f4
- [x] 2.2 Unit test: `AdminUserSeeder.SeedAsync` called twice yields exactly one Administrator user — b4513d3
- [x] 2.3 Unit test: `AdminUserSeeder.SeedAsync` does not throw when `Admin:Email`/`Admin:Password` are unset — b4513d3

#### Manual

- [x] 2.4 Logging in with configured `Admin:Email`/`Admin:Password` succeeds and is recognized as Administrator — 00f03f4

### Phase 3: Lock down public self-registration

#### Automated

- [x] 3.1 Solution builds: `dotnet build melodybooker.sln` — 81d9859
- [x] 3.2 Integration test: anonymous `GET /Identity/Account/Register` does not return 200 — b4513d3
- [x] 3.3 Integration test: Teacher-role `GET /Identity/Account/Register` returns 403 — b4513d3

#### Manual

- [x] 3.4 Logged out, nav shows only "Login" (no "Register") — 399aa58
- [x] 3.5 Logged in as Administrator, nav shows "Register" and the page loads — 399aa58
- [ ] 3.6 Logged in as Teacher, nav hides "Register" and the direct URL shows Access Denied (awaits S-01 teacher account creation; no way to create a Teacher user yet)

### Phase 4: Role-scoped landing stubs

#### Automated

- [x] 4.1 Solution builds: `dotnet build melodybooker.sln` — 18a6381
- [x] 4.2 Integration test: Administrator `GET /Admin` → 200, `GET /Teacher` → 403 — b4513d3
- [x] 4.3 Integration test: Teacher `GET /Teacher` → 200, `GET /Admin` → 403 — b4513d3
- [x] 4.4 Integration test: login redirects each role to its own controller — f5c58a5

#### Manual

- [x] 4.5 Administrator login lands on `/Admin` placeholder page — dae7339
- [ ] 4.6 Teacher login lands on `/Teacher` placeholder page (awaits S-01 teacher account creation; no way to create a Teacher user yet)
- [ ] 4.7 Each role gets Access Denied on the other role's URL (awaits S-01 teacher account creation; no way to create a Teacher user yet)

### Phase 5: Automated verification

#### Automated

- [x] 5.1 `dotnet test` passes with all new tests green — b4513d3
- [x] 5.2 `dotnet build melodybooker.sln` succeeds with the new test project registered — b4513d3

#### Manual

- [x] 5.3 `dotnet test --filter FullyQualifiedName~AuthorizationTests` runs in isolation and passes — b4513d3
- [x] 5.4 `dotnet test --filter FullyQualifiedName~AdminUserSeederTests` runs in isolation and passes — b4513d3
