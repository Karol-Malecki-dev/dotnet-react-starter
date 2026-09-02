import { expect, type Page } from '@playwright/test';

export class RegisterPage {
  constructor(private readonly page: Page) {}

  async open() {
    await this.page.goto('/register');
    await expect(this.page.getByRole('heading', { name: 'Register' })).toBeVisible();
  }

  async register(email: string, password: string) {
    await this.page.getByLabel('First name').fill('E2E');
    await this.page.getByLabel('Last name').fill('Tester');
    await this.page.getByRole('textbox', { name: 'Email', exact: true }).fill(email);
    await this.page.getByLabel('Phone').fill('+48 123 456 789');
    await this.page.getByLabel('Address').fill('E2E Test Address');
    await this.page.locator('input[name="password"]').fill(password);
    await this.page.getByRole('button', { name: 'Create account' }).click();
    await expect(this.page).toHaveURL(/\/login$/);
  }
}

export class ConfirmEmailPage {
  constructor(private readonly page: Page) {}

  async open(link: string) {
    await this.page.goto(link);
    await expect(this.page.getByRole('heading', { name: 'Email confirmed' })).toBeVisible();
  }
}

export class LoginPage {
  constructor(private readonly page: Page) {}

  async open() {
    await this.page.goto('/login');
    await expect(this.page.getByRole('heading', { name: 'Log in' })).toBeVisible();
  }

  async login(email: string, password: string) {
    await this.page.getByRole('textbox', { name: 'Email', exact: true }).fill(email);
    await this.page.getByRole('textbox', { name: 'Password', exact: true }).fill(password);
    await this.page.getByRole('button', { name: 'Sign in' }).click();
    await expect(this.page).toHaveURL(/\/verify-2fa$/);
  }
}

export class TwoFactorPage {
  constructor(private readonly page: Page) {}

  async verify(code: string) {
    const verifyButton = this.page.getByRole('button', { name: 'Verify code' });
    const transportError = this.page.getByText('Failed to fetch', { exact: true });
    await this.page.getByLabel('Verification code').fill(code);

    for (let attempt = 0; attempt < 2; attempt += 1) {
      await verifyButton.click();

      try {
        await expect(this.page).toHaveURL(/\/dashboard$/);
        return;
      } catch (error) {
        // Retry only when Chromium never delivered the request to the API.
        if (attempt > 0 || !(await transportError.isVisible())) {
          throw error;
        }

        await expect(verifyButton).toBeEnabled();
      }
    }
  }
}