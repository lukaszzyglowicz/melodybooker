import { test, expect } from '@playwright/test';

// Risk: auth-gate redirect (no qualifying browser-level risk in
// context/foundation/test-plan.md — every documented risk's cheapest layer is
// unit/integration/process-gate, so this seed proves the generic auth gate
// instead of a specific PRD risk). This test signs in through the UI itself,
// so it opts out of the shared saved session.
test.use({ storageState: { cookies: [], origins: [] } });

test('an unauthenticated visitor is redirected to login, then reaches the original page after signing in', async ({ page }) => {
  const username = process.env.E2E_USERNAME;
  const password = process.env.E2E_PASSWORD;
  if (!username || !password) {
    throw new Error('Set E2E_USERNAME and E2E_PASSWORD (see context/foundation/test-stack.md, ## E2E)');
  }

  await page.goto('/');

  // The app's fallback authorization policy requires an authenticated user
  // everywhere except /health, so the home page redirects to login with a
  // return URL.
  await page.waitForURL((url) => url.pathname === '/Identity/Account/Login');
  await expect(page).toHaveURL(/ReturnUrl=%2F(?:$|&)/);

  await expect(async () => {
    await page.getByLabel('Email').fill('');
    await page.getByLabel('Email').fill(username);
    await page.getByLabel('Password', { exact: true }).fill('');
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Log in' }).click();
    await page.waitForURL((url) => !url.pathname.startsWith('/Identity/Account/Login'), { timeout: 5_000 });
  }).toPass();

  // A signed-in-only element proves the redirect round-trip actually landed
  // on an authenticated page, not just "somewhere that isn't the login form".
  await expect(page.getByRole('link', { name: `Hello ${username}!` })).toBeVisible();

  // No data was created by this flow, so there is nothing to clean up.
});
