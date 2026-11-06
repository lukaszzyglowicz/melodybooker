# Test stack

## E2E

<!-- Written by /10x-e2e-setup. Re-run it to change this section; other skills only read it. -->

- runner: Playwright Test, @playwright/test 1.65.0
- config: playwright.config.ts
- single-spec command: npx playwright test tests/e2e/seed.spec.ts
- full-suite command: npx playwright test
- base URL: http://localhost:5179
- port: 5179 (detected from src/melody/Properties/launchSettings.json "http" profile; override with E2E_PORT)
- web server command: dotnet build melodybooker.sln --configuration Release && dotnet run --project src/melody/melody.csproj --configuration Release --no-build --urls http://localhost:$E2E_PORT; reuseExistingServer outside CI; ASPNETCORE_ENVIRONMENT=Development so it reads appsettings.Development.json (SQL Server LocalDB) and the dotnet user-secrets-configured admin account
- auth setup project: setup (tests/e2e/auth.setup.ts), credentials from E2E_USERNAME / E2E_PASSWORD in .env
- storageState: playwright/.auth/user.json (gitignored)
- seed: tests/e2e/seed.spec.ts — protects auth-gate (no test-plan.md risk has e2e as its cheapest layer; this seed proves the generic "unauthenticated visitor redirected to login, reaches original page after signing in" smoke path instead)
- browser CLI: playwright-cli, command skill at .github/skills/playwright-cli/SKILL.md
- updated: 2026-11-06
