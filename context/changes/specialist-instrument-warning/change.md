---
change_id: specialist-instrument-warning
title: System warns on specialist-instrument mismatch
status: impl_reviewed
created: 2026-10-20
updated: 2026-10-21
archived_at: null
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

- Commit `73046bf` (Phase 2) also bundles unrelated CI tooling added mid-session: `.github/scripts/sync-roadmap-issues.py`, `.github/workflows/sync-roadmap-issues.yml`, and a `context/foundation/roadmap.md` edit. These are out of this plan's scope — they auto-close GitHub issues when a roadmap item is marked `done` — and landed in this SHA only because of how the user's manual commit was staged, not because Phase 2 required them.
