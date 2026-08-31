import { expect, type APIRequestContext } from '@playwright/test';

interface MailpitMessageSummary {
  ID: string;
  To: Array<{ Address: string }>;
}

interface MailpitMessageList {
  messages: MailpitMessageSummary[];
}

interface MailpitMessage {
  Text: string;
  HTML: string;
}

export async function waitForMailpitMessage(
  request: APIRequestContext,
  recipient: string,
  subjectPart: string,
): Promise<MailpitMessage> {
  await expect.poll(async () => {
    const response = await request.get(`${process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025'}/api/v1/messages`);
    if (!response.ok()) {
      return false;
    }

    const payload = (await response.json()) as MailpitMessageList;
    return payload.messages.some(message =>
      message.To.some(address => address.Address.toLowerCase() === recipient.toLowerCase()),
    );
  }, { timeout: 20_000 }).toBe(true);

  const response = await request.get(`${process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025'}/api/v1/messages`);
  await expect(response).toBeOK();
  const payload = (await response.json()) as MailpitMessageList;
  const summary = payload.messages.find(message =>
    message.To.some(address => address.Address.toLowerCase() === recipient.toLowerCase()),
  );
  expect(summary, `No Mailpit message found for ${recipient}`).toBeTruthy();

  const messageResponse = await request.get(
    `${process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025'}/api/v1/message/${summary!.ID}`,
  );
  await expect(messageResponse).toBeOK();
  const message = (await messageResponse.json()) as MailpitMessage;
  expect(`${message.Text}\n${message.HTML}`).toContain(subjectPart);
  return message;
}

export function extractConfirmationLink(message: MailpitMessage): string {
  const content = `${message.Text}\n${message.HTML}`;
  const match = content.match(/https?:\/\/[^\s"'<>]+\/confirm-email\?userId=[^\s"'<>]+/);
  expect(match, 'Confirmation link was not found in Mailpit message').toBeTruthy();
  return match![0].replace(/&amp;/g, '&');
}

export function extractTwoFactorCode(message: MailpitMessage): string {
  const match = `${message.Text}\n${message.HTML}`.match(/\b\d{6}\b/);
  expect(match, 'Two-factor code was not found in Mailpit message').toBeTruthy();
  return match![0];
}