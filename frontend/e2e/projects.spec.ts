import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { ConfirmEmailPage, LoginPage, RegisterPage, TwoFactorPage } from './pages/AuthPages';
import { extractConfirmationLink, extractTwoFactorCode, waitForMailpitMessage } from './support/mailpit';

async function registerAndSignIn(page: Page, request: APIRequestContext) {
  const email = `project-e2e-${Date.now()}@example.test`;
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
}

test('create a project and complete the task collaboration workflow', async ({ page, request }) => {
  await registerAndSignIn(page, request);
  const suffix = Date.now().toString();
  const projectName = `Release project ${suffix}`;
  const taskTitle = `Release task ${suffix}`;
  const comment = `Browser verification ${suffix}`;

  await page.getByRole('link', { name: 'Projects' }).first().click();
  await page.getByLabel('Name').fill(projectName);
  await page.getByLabel('Description').fill('V4 browser release workflow');
  await page.getByRole('button', { name: 'Create project' }).click();
  await expect(page.getByRole('heading', { name: projectName })).toBeVisible();

  await page.getByLabel('Task title').fill(taskTitle);
  await page.getByLabel('Description').last().fill('Task exercised by Playwright');
  await page.getByRole('button', { name: 'Add task' }).click();
  const task = page.locator('.task-item').filter({ hasText: taskTitle });
  await expect(task).toBeVisible();

  await task.getByLabel(`Status for ${taskTitle}`).selectOption({ label: 'Done' });
  await expect(task.getByLabel(`Status for ${taskTitle}`)).toHaveValue('2');

  await task.getByRole('button', { name: 'Discussion' }).click();
  await page.getByLabel('Add a comment').fill(comment);
  await page.getByRole('button', { name: 'Post comment' }).click();
  await expect(page.getByText(comment)).toBeVisible();

  await task.getByRole('button', { name: 'Attachments' }).click();
  await page.getByLabel('Choose a file').setInputFiles({
    name: 'release-note.txt',
    mimeType: 'text/plain',
    buffer: Buffer.from('V4 release attachment verification\n', 'utf8'),
  });
  await page.getByRole('button', { name: 'Upload attachment' }).click();
  const attachment = page.locator('.task-attachments__item').filter({ hasText: 'release-note.txt' });
  await expect(attachment).toBeVisible();

  const downloadPromise = page.waitForEvent('download');
  await attachment.getByRole('button', { name: 'Download' }).click();
  const download = await downloadPromise;
  expect(download.suggestedFilename()).toBe('release-note.txt');

  await attachment.getByRole('button', { name: 'Delete' }).click();
  await expect(attachment).toHaveCount(0);
});
