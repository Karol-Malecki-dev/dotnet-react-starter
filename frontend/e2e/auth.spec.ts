import { test } from '@playwright/test';
import { ConfirmEmailPage, LoginPage, RegisterPage, TwoFactorPage } from './pages/AuthPages';
import { extractConfirmationLink, extractTwoFactorCode, waitForMailpitMessage } from './support/mailpit';

test('register, confirm email, complete 2FA, and log out', async ({ page, request }) => {
  const email = `e2e-${Date.now()}@example.test`;
  const password = 'E2e.Password.123!';

  const registerPage = new RegisterPage(page);
  await registerPage.open();
  await registerPage.register(email, password);

  const confirmationMessage = await waitForMailpitMessage(request, email, 'Confirm');
  await new ConfirmEmailPage(page).open(extractConfirmationLink(confirmationMessage));

  const loginPage = new LoginPage(page);
  await loginPage.open();
  await loginPage.login(email, password);

  const twoFactorMessage = await waitForMailpitMessage(request, email, 'verification');
  await new TwoFactorPage(page).verify(extractTwoFactorCode(twoFactorMessage));

  await page.getByRole('button', { name: /logout/i }).click();
  await page.waitForURL(/\/$/);
});