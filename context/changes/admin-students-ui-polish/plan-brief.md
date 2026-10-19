# Admin Students Roster UI Polish — Plan Brief

> Full plan: `context/changes/admin-students-ui-polish/plan.md`

## What & Why

The admin students roster view (`Views/AdminStudents`) works but carries UI debt found by a `/10x-ui` audit: a color override that's silently dead for every page (CSS-isolation bug), duplicated form markup between Create/Edit, and action links a screen reader can't tell apart. This change fixes the token source, removes the duplication, and adds accessible names — no new dependency, no new design system.

## Starting Point

Plain Bootstrap 5.3 scaffold, no component library or design-token layer beyond Bootstrap's own `--bs-*` variables. `_Layout.cshtml.css` hardcodes three button/link colors that, due to ASP.NET Core CSS isolation, never actually apply outside the layout's own markup — every content-view button has been silently rendering Bootstrap's default blue instead of the intended `#1b6ec2`. `Create.cshtml`/`Edit.cshtml` duplicate the same 4-field form block verbatim, and `Index.cshtml`'s Edit/Delete links share the same unqualified text across every row.

## Desired End State

One `--brand-primary` CSS variable (defined once in `site.css`) backs every button, link, and focus-ring color sitewide. The intended brand blue finally renders. `Create`/`Edit` share one form-fields partial. Every roster row's Edit/Delete link has a distinct accessible name. `AGENTS.md` tells the next agent where the token and partial live.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Brand color value | Keep `#1b6ec2`, just centralize it | Zero new color choice to review; the user wanted the existing value made consistent, not replaced | Plan (user Q&A) |
| Fix scope | `AdminStudents` only, not `AdminTeachers` | Stays inside the "one view + global tokens" rule; `AdminTeachers` has the same duplication but gets its own change later | Plan (user Q&A) |
| Visual gate | Manual screenshot check only, no new tooling | No Playwright exists yet; adding one isn't worth it for an internal admin-only screen on a 3-week MVP budget | Plan (user Q&A) |
| Dead CSS-isolation override | Fix it as part of "centralize the token" | Centralizing a color that's currently inert everywhere outside the layout would still be a no-op without this fix | Plan (research) |

## Scope

**In scope:**
- `site.css` / `_Layout.cshtml.css` color-rule consolidation into one `--brand-primary` variable
- `AdminStudents/Create.cshtml` + `Edit.cshtml` shared form-fields partial
- `AdminStudents/Index.cshtml` row-action `aria-label`s
- `AGENTS.md` UI-conventions guard

**Out of scope:**
- `AdminTeachers` views (same duplication pattern, deferred)
- Any new design-token system, Sass pipeline, or component library
- A new brand color choice
- Playwright / kitchen-sink / any new visual-regression tooling
- Any booking/business-logic change

## Architecture / Approach

Token → view → states, per the UI skill's phase order. Phase 1 moves the color rules from the CSS-isolation-scoped `_Layout.cshtml.css` into the global `site.css`, backed by one custom property — fixing the dead override as a side effect of centralizing it. Phase 2 extracts the duplicated form partial and adds the `aria-label`s, confined to `AdminStudents`. Phase 3 is the manual verification pass (screenshots, hardcoded-value re-scan, 7-state matrix with N/A reasoning) plus the `AGENTS.md` guard so the fix sticks.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Centralize the brand color token | One `--brand-primary` variable; dead override fixed sitewide | Buttons visibly change color sitewide for the first time (intended, but easy to mistake for a regression if not flagged) |
| 2. Shared form partial + accessible action names | `_StudentFormFields.cshtml` partial; per-row `aria-label`s | Partial extraction must not alter rendered markup (validation spans, labels) |
| 3. Verification pass + agent-rules guard | Screenshots, re-scan, 7-state record, `AGENTS.md` section | Easy to skip the N/A-with-reason discipline on states that don't obviously apply |

**Prerequisites:** none — builds directly on the current `main`/working-branch state, no other change blocks this.
**Estimated effort:** ~1 session across 3 phases (small CSS + 2 view files + 1 new partial + doc update).

## Open Risks & Assumptions

- Assumes the CSS-isolation diagnosis (dead `_Layout.cshtml.css` override) is confirmed correct — verified via documented ASP.NET Core scoping behavior and cross-checked against the compiled `bootstrap.css`, but should still be visually confirmed in Phase 1's manual check since it's the riskiest assumption in the plan.
- Fixing the dead override changes button color **sitewide**, not just on `AdminStudents` — acceptable per the "global tokens" allowance, but worth a heads-up during review since it's a bigger visible diff than "one view" might suggest.

## Success Criteria (Summary)

- An administrator sees the intended brand-blue buttons consistently across `AdminStudents` (and sitewide), not a silently-wrong Bootstrap default.
- Editing a student form field (label, validation, new field) requires touching one partial, not two pages.
- A screen-reader user can distinguish "Edit Jan Kowalski" from "Edit Anna Nowak" in the roster table.
