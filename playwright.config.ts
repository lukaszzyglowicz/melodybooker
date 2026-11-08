import { existsSync } from 'node:fs';
import { defineConfig, devices } from '@playwright/test';

// Local secrets (E2E_USERNAME, E2E_PASSWORD) come from the gitignored .env file.
// In CI the file is absent and the variables come from the job instead.
if (existsSync('.env')) process.loadEnvFile('.env');

// 5179 was detected from src/melody/Properties/launchSettings.json ("http" profile).
// E2E_PORT overrides it when that port is taken on this machine.
const PORT = Number(process.env.E2E_PORT ?? 5179);
const baseURL = `http://localhost:${PORT}`;

export default defineConfig({
  testDir: './tests/e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'setup', testMatch: /.*\.setup\.ts/ },
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], storageState: 'playwright/.auth/user.json' },
      dependencies: ['setup'],
    },
  ],
  webServer: {
    // Production-like build + run, on the port above. ASPNETCORE_ENVIRONMENT=Development
    // makes it read appsettings.Development.json (LocalDB connection string) and the
    // dotnet user-secrets-configured admin account, same as local `dotnet run`.
    command: `dotnet build melodybooker.sln --configuration Release && dotnet run --project src/melody/melody.csproj --configuration Release --no-build --urls http://localhost:${PORT}`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    env: { ASPNETCORE_ENVIRONMENT: 'Development' },
  },
});
