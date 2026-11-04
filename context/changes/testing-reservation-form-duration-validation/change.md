---
change_id: testing-reservation-form-duration-validation
title: Reservation form & duration validation hardening (rollout Phase 3)
status: impl_reviewed
created: 2026-11-04
updated: 2026-11-04
archived_at: null
---

## Notes

Open a change folder for rollout Phase 3 of context/foundation/test-plan.md: "Reservation form & duration validation hardening".
Risks covered: #4 (reservation-form validation - student class x instrument x room - silently breaks one condition while fixing another), #5 (lesson-duration derivation mishandles the class 4/5 boundary or a student with no assigned class). Test types planned: unit.
Risk response intent:
- Risk #4: prove every student-class x instrument x room-type combination yields the correct computed (duration, hard-block, soft-warning) per FR-009/FR-011, and that changing one rule does not silently alter another's output; challenge "no validation error shown" does not mean "the correct value was computed/persisted"; avoid one large integration test that confirms "it works" without pinpointing which field combination regressed.
- Risk #5: prove every class 1-8 maps to the correct duration (20 vs 40 min) and a student with no assigned class produces an explicit validation error, not a silent default; challenge testing only one representative class per duration bucket, which misses the 4/5 boundary and the null-class case; avoid covering one class value per duration bucket and skipping the boundary.
After creating the folder, follow the downstream continuation rule.
