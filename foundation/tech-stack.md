---
starter_id: dotnet
package_manager: dotnet
project_name: melody-booker
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: standard
  quality_override: false
  self_check_answers: null
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
---

## Why this stack

MelodyBooker is a medium-scale web app with a 3-week after-hours timeline and a login-gated room-reservation workflow (auth in scope; payments explicitly out per PRD non-goals). `.NET (ASP.NET Core webapi)` is the recommended default for `(web, dotnet)`: strongly typed end-to-end, convention-based project layout, popular within the .NET training corpus, and well-documented via Microsoft Learn — it clears all four agent-friendly gates, so no custom-path interview was needed. Bootstrapper confidence is verified, meaning scaffolding should go smoothly. Deployment targets Azure App Service, the starter's own default; CI runs on GitHub Actions with auto-deploy on merge to main, matching the solo/after-hours delivery profile.
