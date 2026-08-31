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

test('invite a viewer and enforce read-only project permissions', async ({ browser, page, request }, testInfo) => {
  const suffix = Date.now().toString();
  const viewerEmail = `project-viewer-${suffix}@example.test`;
  const ownerEmail = `project-owner-${suffix}@example.test`;
  const password = 'E2e.Password.123!';
  const projectName = `Viewer project ${suffix}`;
  const taskTitle = `Owner task ${suffix}`;
  const viewerContext = await browser.newContext({ baseURL: testInfo.project.use.baseURL as string });
  const viewerPage = await viewerContext.newPage();

  try {
    const viewerRegisterPage = new RegisterPage(viewerPage);
    await viewerRegisterPage.open();
    await viewerRegisterPage.register(viewerEmail, password);
    const viewerConfirmation = await waitForMailpitMessage(request, viewerEmail, 'Confirm');
    await new ConfirmEmailPage(viewerPage).open(extractConfirmationLink(viewerConfirmation));
    const viewerLoginPage = new LoginPage(viewerPage);
    await viewerLoginPage.open();
    await viewerLoginPage.login(viewerEmail, password);
    const viewerTwoFactor = await waitForMailpitMessage(request, viewerEmail, 'verification');
    await new TwoFactorPage(viewerPage).verify(extractTwoFactorCode(viewerTwoFactor));

    const ownerRegisterPage = new RegisterPage(page);
    await ownerRegisterPage.open();
    await ownerRegisterPage.register(ownerEmail, password);
    const ownerConfirmation = await waitForMailpitMessage(request, ownerEmail, 'Confirm');
    await new ConfirmEmailPage(page).open(extractConfirmationLink(ownerConfirmation));
    const ownerLoginPage = new LoginPage(page);
    await ownerLoginPage.open();
    await ownerLoginPage.login(ownerEmail, password);
    const ownerTwoFactor = await waitForMailpitMessage(request, ownerEmail, 'verification');
    await new TwoFactorPage(page).verify(extractTwoFactorCode(ownerTwoFactor));

    await page.getByRole('link', { name: 'Projects' }).first().click();
    await page.getByLabel('Name').fill(projectName);
    await page.getByRole('button', { name: 'Create project' }).click();
    await page.getByLabel('Task title').fill(taskTitle);
    await page.getByRole('button', { name: 'Add task' }).click();
    await page.getByLabel('Account email').fill(viewerEmail);
    await page.getByLabel('Role', { exact: true }).selectOption({ label: 'Viewer' });
    await page.getByRole('button', { name: 'Create invitation' }).click();
    const invitationLink = await page.getByLabel('Invitation link').inputValue();

    await viewerPage.goto(invitationLink);
    await viewerPage.getByRole('button', { name: 'Accept invitation' }).click();
    await expect(viewerPage.getByText('You joined the project.')).toBeVisible();
    await viewerPage.getByRole('link', { name: 'Projects' }).first().click();
    await viewerPage.getByRole('button', { name: new RegExp(projectName) }).click();

    await expect(viewerPage.getByRole('heading', { name: 'Add task' })).toHaveCount(0);
    const viewerTask = viewerPage.locator('.task-item').filter({ hasText: taskTitle });
    await expect(viewerTask.getByLabel(`Status for ${taskTitle}`)).toBeDisabled();
    await viewerTask.getByRole('button', { name: 'Discussion' }).click();
    await expect(viewerPage.getByText('Viewers can read comments but cannot add them.')).toBeVisible();
    await viewerTask.getByRole('button', { name: 'Attachments' }).click();
    await expect(viewerPage.getByText('Viewers can download attachments but cannot upload them.')).toBeVisible();
  } finally {
    await viewerContext.close();
  }
});
