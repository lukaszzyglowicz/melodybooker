# Admin Students Roster UI Polish Implementation Plan

## Overview

Fix three UI-contract gaps found during the `/10x-ui` audit of `src/melody/Views/AdminStudents`: a dead color override (CSS-isolation bug), duplicated form markup between `Create.cshtml`/`Edit.cshtml`, and row-action links with no accessible name. No new dependency, no new design system — the fix centralizes the existing brand color and removes duplication within the Bootstrap stack already in the repo.

## Current State Analysis

- **Stack**: plain Bootstrap 5.3 (`wwwroot/lib/bootstrap`), no Tailwind/shadcn, no CSS-in-JS. Value source = Bootstrap's own `--bs-*` CSS custom properties shipped in `bootstrap.css`. Shared components dir = none (`Views/Shared/` holds only layout partials).
- **Dead CSS-isolation override (new discovery, confirmed this session)**: `Views/Shared/_Layout.cshtml.css` sets `.btn-primary`, `a`, and `.nav-pills .nav-link.active` to hardcoded hex (`#1b6ec2`/`#1861ac`, `#0077cc`). ASP.NET Core's CSS isolation scopes a layout's `.cshtml.css` rules **only to markup literally written in that `.cshtml` file** — it does not reach content rendered through `@RenderBody()`. None of `_Layout.cshtml`'s own markup uses `.btn-primary`/`.nav-pills`, and the only `a` tags in it are the navbar brand/nav links (which already inherit Bootstrap's native link color visually). Net effect: every button rendered by a content view — including `AdminStudents`'s "Add student"/"Create"/"Save" buttons — has been rendering Bootstrap's **default** blue (`--bs-btn-bg: #0d6efd`), not the intended `#1b6ec2`, the whole time. This is a real (if invisible) bug, not just a style preference.
- **Three uncoordinated hardcoded blues**: `_Layout.cshtml.css:7` (`#0077cc`, dead per above), `_Layout.cshtml.css:11-14` (`#1b6ec2`/`#1861ac`, dead per above), `wwwroot/css/site.css:8` (`#258cfb`, this one **is** live — it's in the global, unscoped `site.css`, and already governs the focus-visible ring sitewide).
- **Duplicated form fields**: `Views/AdminStudents/Create.cshtml:16-36` and `Views/AdminStudents/Edit.cshtml:11-28` repeat the identical 4-field block (`FullName`, `Class`, `Instrument`, `TeacherId`) verbatim. No shared partial exists today; a field change (label, validation layout, new field) must be hand-synced in two files.
- **Ambiguous action link names**: `Views/AdminStudents/Index.cshtml:30-33` — every row's Edit/Delete links share the literal text "Edit"/"Delete" with no accessible name tying them to a specific student. A screen-reader user navigating by links hears "Edit, Edit, Edit…" with no way to tell rows apart.
- **AGENTS.md** currently has no UI-specific rule block — nothing points a future agent at where the color lives or that a shared partial exists for this form.
- **No visual-regression tooling** exists in the repo (no Playwright/test project wired to UI). Verification for this change is manual (desktop + one mobile width), per user decision.

### Key Discoveries:

- `_Layout.cshtml.css:11-14`'s `.btn-primary` override is inert for every content view (confirmed via ASP.NET Core CSS-isolation scoping rules — the scope attribute is only applied to the file's own rendered elements, never cascading into `@RenderBody()` content).
- Bootstrap's shipped `--bs-primary` root custom property (`bootstrap.css`: `--bs-primary: #0d6efd;`) is **not** actually consumed by `.btn-primary` — `.btn-primary`'s own component-level `--bs-btn-bg`/`--bs-btn-border-color` are pre-baked literals in the compiled `bootstrap.css`, not `var(--bs-primary)` references. Overriding Bootstrap's own token would be a no-op; the project needs its own custom property instead.
- `site.css` is the only global (non-isolated) stylesheet loaded by `_Layout.cshtml` (`<link ... href="~/css/site.css" />`), making it the correct home for any rule that must affect content-view markup.

## Desired End State

- One project-level CSS custom property (`--brand-primary`, defined once in `site.css`) backs the button background/border, the link color, and the focus-ring color. Changing the brand color going forward means editing one value.
- The intended brand blue (`#1b6ec2`) actually renders on every button sitewide (fixing the dead-override bug), including `AdminStudents`'s Add/Create/Save buttons.
- `Create.cshtml` and `Edit.cshtml` both render their 4 form fields from one shared partial; editing a field edits one file.
- Each Edit/Delete link in the students table has an accessible name identifying which student it acts on.
- `AGENTS.md` has a short UI rule block naming the color variable and the shared partial, so future agents don't reintroduce the duplication or a fourth hardcoded blue.

### Automated verification covers: `dotnet build` succeeds; a `grep` re-scan for raw hex/`rgba()` in `Views/AdminStudents/*.cshtml` and `wwwroot/css/site.css`/`Views/Shared/_Layout.cshtml.css` shows the hex count for brand-blue literals dropped to the single `:root` definition.
### Manual verification covers: desktop + one mobile-width screenshot of `AdminStudents/Index`, `Create`, and `Edit` showing the corrected button color and unchanged layout; screen-reader (or browser accessibility tree inspection) confirms distinct Edit/Delete names per row.

## What We're NOT Doing

- Not touching `AdminTeachers` views, even though they have the identical duplicated-form-fields pattern (explicitly scoped out — stays inside "one view + global tokens", follow-up change if desired).
- Not introducing a new design-token system, Sass build pipeline, or component library — this stays inside the existing Bootstrap + CSS-isolation setup.
- Not picking a new brand color — `#1b6ec2` (the value already written, if previously inert, in `_Layout.cshtml.css`) is preserved as the single source value.
- Not adding Playwright, a kitchen-sink page, or any new visual-regression tooling — verification is manual screenshot comparison only.
- Not hard-blocking or otherwise changing any booking/business logic — this change is UI-only.

## Implementation Approach

Fix the color at its root cause (move the isolated, dead rules into the global stylesheet and back them with one custom property) before touching the view markup, then clean up the one view's duplication and accessibility gap, then verify and leave a guard rule. This follows the skill's token → view → states ordering and keeps each phase independently buildable and screenshot-able.

## Phase 1: Centralize the brand color token (fixes dead CSS-isolation override)

### Overview

Move the three color rules out of `_Layout.cshtml.css` (where they're inert for body content) into `site.css` (global, unscoped), and back `.btn-primary`, the link color, and the focus ring with one new `--brand-primary` custom property instead of three repeated hex literals.

### Changes Required:

#### 1. Global color token + button/link/focus rules

**File**: `src/melody/wwwroot/css/site.css`

**Intent**: Define the brand color once and make the button/link/focus rules that currently live in the dead-scoped `_Layout.cshtml.css` actually apply to every rendered view, using the new variable instead of repeated hex.

**Contract**: Add a `:root` block defining `--brand-primary: #1b6ec2;` and `--brand-primary-active: #1861ac;` (the existing hover/active shade) above the existing rules. Add `.btn-primary { background-color: var(--brand-primary); border-color: var(--brand-primary-active); }` and `a { color: var(--brand-primary); }` to `site.css`. Update the existing `.btn:focus, ...` rule's hardcoded `#258cfb` to `var(--brand-primary)` as well, so the focus ring and brand color share one source (previously `#258cfb` was a fourth, uncoordinated blue).

#### 2. Remove the now-redundant dead rules

**File**: `src/melody/Views/Shared/_Layout.cshtml.css`

**Intent**: Delete the `.btn-primary`, `a`, and `.nav-pills .nav-link.active` color rules now that their intended effect lives in `site.css` — leaving them here would be dead, confusing duplication (and the `.nav-pills` rule was never applicable; no `.nav-pills` markup exists in `_Layout.cshtml`, so it can be dropped outright rather than moved).

**Contract**: File keeps only `a.navbar-brand`, `.border-top`, `.border-bottom`, `.box-shadow`, `button.accept-policy`, `.footer` — i.e., rules that only ever needed to affect `_Layout.cshtml`'s own markup.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds
- `grep -c "#1b6ec2\|#1861ac\|#0077cc\|#258cfb"` across `src/melody/wwwroot/css/site.css` and `src/melody/Views/Shared/_Layout.cshtml.css` combined returns exactly 2 (the two `:root` definitions), down from 4 scattered literals

#### Manual Verification:

- Run the app, open `AdminStudents/Index`, `Create`, `Edit`: the "Add student"/"Create"/"Save" buttons now render in brand blue (visibly different from Bootstrap's prior default `#0d6efd`) — confirm this is the intended, expected visual change from fixing the dead override, not a regression
- Keyboard-tab to a button/link/input on any `AdminStudents` page: the focus ring renders in the same brand blue as the buttons

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Shared form partial + accessible action names (AdminStudents)

### Overview

Remove the Create/Edit form-field duplication and give the Index roster's Edit/Delete links a distinct accessible name per row.

### Changes Required:

#### 1. Shared student form fields partial

**File**: `src/melody/Views/AdminStudents/_StudentFormFields.cshtml` (new)

**Intent**: Extract the 4-field block (`FullName`, `Class`, `Instrument`, `TeacherId`) shared verbatim by `Create.cshtml` and `Edit.cshtml` into one partial view so future field changes happen in one place.

**Contract**: Partial takes the page's `StudentFormViewModel` as its implicit model (same `@model` the two pages already declare) and renders the same 4 `form-group mb-3` blocks, including each field's `asp-validation-for` span, exactly as they exist today — no behavior change, pure extraction.

#### 2. Consume the partial from both pages

**File**: `src/melody/Views/AdminStudents/Create.cshtml`, `src/melody/Views/AdminStudents/Edit.cshtml`

**Intent**: Replace each page's inline 4-field block with `<partial name="_StudentFormFields" />`, keeping each page's own wrapping markup (the `Create.cshtml` `<fieldset disabled>` wrapper and `Edit.cshtml`'s hidden `Id` input stay in the parent view, since they aren't shared).

**Contract**: `Create.cshtml` keeps its `<fieldset disabled="@(!hasTeachers)">` wrapper around the partial call; `Edit.cshtml` keeps its `<input type="hidden" asp-for="Id" />` before the partial call. Both pages' submit button and Cancel link stay where they are.

#### 3. Accessible row-action names

**File**: `src/melody/Views/AdminStudents/Index.cshtml`

**Intent**: Give each row's Edit/Delete link an accessible name that includes the student's name, so a screen-reader user navigating by links can tell which student a given link acts on.

**Contract**: Add `aria-label="Edit @student.FullName"` to the Edit `<a>` and `aria-label="Delete @student.FullName"` to the Delete `<a>` in the `@foreach` loop (around line 30-33), leaving the visible "Edit"/"Delete" text unchanged.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds
- `grep -c "FullName" src/melody/Views/AdminStudents/Create.cshtml src/melody/Views/AdminStudents/Edit.cshtml` each show exactly one reference to the field (inside the partial call), confirming the inline block was removed from both

#### Manual Verification:

- `Create` and `Edit` pages render identically to before (same fields, same labels, same validation spans) when manually compared
- Browser accessibility tree (or screen reader) on `AdminStudents/Index` shows each row's Edit/Delete link with a distinct accessible name including the student's name

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Verification pass + agent-rules guard

### Overview

Confirm the fix with the hardcoded-value re-scan and the 7-state matrix (most states N/A for this static CRUD screen, with reasons recorded), capture before/after screenshots at desktop + one mobile width, and add a short UI rule block to `AGENTS.md` so the next agent reuses the token and partial instead of reinventing them.

### Changes Required:

#### 1. Agent rules guard

**File**: `AGENTS.md`

**Intent**: Point future agents at the single color source and the shared form partial, and state the no-literal-colors rule for this app's views, so the fix in Phases 1-2 doesn't get undone by the next feature view.

**Contract**: Append a new `## UI conventions` section (after the existing `## Coding conventions` section) stating: brand color lives in `src/melody/wwwroot/css/site.css`'s `:root` block as `--brand-primary`/`--brand-primary-active` — reference it, don't hardcode hex; shared form-field partials live alongside their controller's views (e.g. `Views/AdminStudents/_StudentFormFields.cshtml`) — check for an existing partial before duplicating a field block; row-action links need an `aria-label` identifying the row's subject.

### Success Criteria:

#### Automated Verification:

- `dotnet build melodybooker.sln` succeeds (final build after all phases)
- Hardcoded-value re-scan (`grep -nE '#[0-9a-fA-F]{3,8}\b|rgba?\('` against `Views/AdminStudents/*.cshtml`) returns 0 matches, same as the pre-audit baseline (no regression)

#### Manual Verification:

- 7-state matrix recorded for `AdminStudents/Index`/`Create`/`Edit`: default/hover/focus-visible/disabled/error all shown and token-driven (Bootstrap + `--brand-primary`); empty state already shown ("No students yet." text, pre-existing); loading — N/A, this is a traditional full-postback MVC app with no client-side async loading state
- Desktop (≥1280px) and one mobile width (~375px) screenshots taken of `Index`, `Create`, `Edit` confirming no layout regression from the partial extraction
- `AGENTS.md`'s new `## UI conventions` section reads correctly and doesn't duplicate or contradict the existing `## Coding conventions` section

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- No unit tests apply — this change is Razor view/CSS markup only, no controller/business logic touched.

### Integration Tests:

- None required; existing `AdminStudentsController`-facing integration tests (if any) are unaffected since no controller code changes.

### Manual Testing Steps:

1. Run `dotnet run --project src/melody/melody.csproj`, log in as Administrator, open `/AdminStudents`.
2. Confirm the "Add student" button, and the `Create`/`Edit` pages' submit buttons, render in the brand blue.
3. Tab through each page's controls and confirm the focus ring matches the brand blue.
4. Compare `Create.cshtml`/`Edit.cshtml` field rendering before/after — identical labels, inputs, validation spans.
5. Inspect the Index page's row links (via browser dev tools accessibility pane) and confirm distinct per-row accessible names.
6. Resize to a ~375px mobile width and re-check all three pages for layout regressions.

## Performance Considerations

None — this is a CSS/markup-only change with no new requests, assets, or render-blocking additions.

## Migration Notes

Not applicable — no data model or schema changes.

## References

- Pre-audit findings: this session's `/10x-ui` invocation (see conversation history; no separate `research.md` was generated for this change)
- `src/melody/Views/AdminStudents/Index.cshtml:30-33` — row action links
- `src/melody/Views/AdminStudents/Create.cshtml:16-36`, `src/melody/Views/AdminStudents/Edit.cshtml:11-28` — duplicated form fields
- `src/melody/Views/Shared/_Layout.cshtml.css:7,11-14` — dead CSS-isolation color rules
- `src/melody/wwwroot/css/site.css:8` — existing live focus-ring rule
- `src/melody/wwwroot/lib/bootstrap/dist/css/bootstrap.css` — confirms `.btn-primary`'s component-level custom properties are pre-baked literals, not `var(--bs-primary)` references

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Centralize the brand color token (fixes dead CSS-isolation override)

#### Automated

- [x] 1.1 `dotnet build melodybooker.sln` succeeds (8189b71)
- [x] 1.2 Hex literal count across `site.css` + `_Layout.cshtml.css` drops to 2 (the two `:root` definitions) (8189b71)

#### Manual

- [x] 1.3 Buttons on AdminStudents pages render in brand blue (visible fix of the dead override) (8189b71)
- [x] 1.4 Focus ring matches brand blue on keyboard tab (8189b71)

### Phase 2: Shared form partial + accessible action names (AdminStudents)

#### Automated

- [x] 2.1 `dotnet build melodybooker.sln` succeeds (fefd33e)
- [x] 2.2 `Create.cshtml`/`Edit.cshtml` each reference `FullName` exactly once (inline block removed) (fefd33e)

#### Manual

- [x] 2.3 Create/Edit pages render identically to before (fefd33e)
- [x] 2.4 Index row links have distinct per-student accessible names (fefd33e)

### Phase 3: Verification pass + agent-rules guard

#### Automated

- [x] 3.1 Final `dotnet build melodybooker.sln` succeeds (9488f37)
- [x] 3.2 Hardcoded-value re-scan on `Views/AdminStudents/*.cshtml` returns 0 matches (9488f37)

#### Manual

- [x] 3.3 7-state matrix recorded (states shown or N/A with reason) (9488f37)
- [x] 3.4 Desktop + mobile screenshots taken, no layout regression (9488f37)
- [x] 3.5 `AGENTS.md` UI conventions section reads correctly (9488f37)
