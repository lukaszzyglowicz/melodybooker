---
starter_id: dotnet
package_manager: dotnet
project_name: melodybooker
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: true
---

## Why this stack

Solo developer building a music-school room-booking web app within a short
after-hours MVP timeline, with authentication and weekly-recurring scheduling
(treated as a background-jobs concern) in scope. The user chose the custom
path and confirmed a preference for SQL Server, which pairs naturally with
this starter's Entity Framework integration. .NET (ASP.NET Core webapi) is
the only registry candidate for the `(web-app, dotnet)` cell and clears all
four agent-friendly gates — explicit C# typing, convention-based ASP.NET
Core structure, strong popularity within .NET training data, and current
Microsoft Learn documentation. Bootstrapper confidence is verified, so
scaffolding is expected to be smooth. Deployment defaults to Azure App
Service per the starter's card; CI runs on GitHub Actions with
auto-deploy-on-merge. The five-point self-check came back clean (0 of 5
flagged), so no Socratic nudge fired. Background-job support (recurring
weekly bookings) isn't first-class in the card but is a well-known .NET
ecosystem pattern bootstrapper can add manually.
