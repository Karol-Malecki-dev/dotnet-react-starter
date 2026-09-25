import { waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { HttpClient } from '../../../services/api/HttpClient';
import { tokenManager } from '../../../services/api/TokenManager';

function createJsonResponse(status: number, body: unknown): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    statusText: status === 401 ? 'Unauthorized' : 'OK',
    headers: new Headers({ 'content-type': 'application/json' }),
    json: async () => body,
    text: async () => JSON.stringify(body),
    blob: async () => new Blob(),
  } as Response;
}

describe('HttpClient unauthorized request coordination', () => {
  const fetchMock = vi.fn<(input: RequestInfo | URL, init?: RequestInit) => Promise<Response>>();

  beforeEach(() => {
    tokenManager.clearSession();
    vi.stubGlobal('fetch', fetchMock);
    fetchMock.mockReset();
  });

  afterEach(() => {
    tokenManager.clearSession();
    vi.unstubAllGlobals();
  });

  it('shares one refresh promise across concurrent unauthorized requests', async () => {
    const refreshedTokens = {
      accessToken: 'fresh-token',
      expiresIn: 900,
    };
    let releaseInitialResponses!: () => void;
    let releaseRefresh!: () => void;
    let initialUnauthorizedResponses = 0;

    const initialResponsesReleased = new Promise<void>((resolve) => {
      releaseInitialResponses = resolve;
    });
    const refreshPromise = new Promise<boolean>((resolve) => {
      releaseRefresh = () => {
        tokenManager.setSession(refreshedTokens);
        resolve(true);
      };
    });
    const onUnauthorized = vi.fn(() => refreshPromise);
    const authorizationHeaders: string[] = [];

    fetchMock.mockImplementation(async (_input, init) => {
      const authorization = new Headers(init?.headers).get('Authorization') ?? '';
      authorizationHeaders.push(authorization);

      if (authorization === 'Bearer expired-token') {
        initialUnauthorizedResponses += 1;
        if (initialUnauthorizedResponses === 2) {
          releaseInitialResponses();
        }

        await initialResponsesReleased;
        return createJsonResponse(401, { message: 'Token expired' });
      }

      return createJsonResponse(200, { ok: true });
    });

    tokenManager.setSession({ accessToken: 'expired-token', expiresIn: 0 });
    const client = new HttpClient({
      baseUrl: 'http://api.test',
      onUnauthorized,
    });

    const requests = Promise.all([
      client.get<{ ok: boolean }>('/projects'),
      client.get<{ ok: boolean }>('/notifications'),
    ]);

    await waitFor(() => expect(initialUnauthorizedResponses).toBe(2));
    await waitFor(() => expect(onUnauthorized).toHaveBeenCalledTimes(1));

    releaseRefresh();

    await expect(requests).resolves.toEqual([{ ok: true }, { ok: true }]);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledTimes(4);
    expect(authorizationHeaders).toEqual([
      'Bearer expired-token',
      'Bearer expired-token',
      'Bearer fresh-token',
      'Bearer fresh-token',
    ]);
  });

  it('retries with a token refreshed by another request without starting another refresh', async () => {
    const refreshedTokens = {
      accessToken: 'fresh-token',
      expiresIn: 900,
    };
    const onUnauthorized = vi.fn(async () => true);
    let requestCount = 0;

    fetchMock.mockImplementation(async (_input, init) => {
      requestCount += 1;
      expect(new Headers(init?.headers).get('Authorization')).toBe(
        requestCount === 1 ? 'Bearer expired-token' : 'Bearer fresh-token',
      );

      if (requestCount === 1) {
        tokenManager.setSession(refreshedTokens);
        return createJsonResponse(401, { message: 'Token expired' });
      }

      return createJsonResponse(200, { ok: true });
    });

    tokenManager.setSession({ accessToken: 'expired-token', expiresIn: 0 });
    const client = new HttpClient({
      baseUrl: 'http://api.test',
      onUnauthorized,
    });

    await expect(client.get<{ ok: boolean }>('/projects')).resolves.toEqual({ ok: true });
    expect(onUnauthorized).not.toHaveBeenCalled();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it('does not refresh or emit authenticated-session handling for skip-auth requests', async () => {
    const onUnauthorized = vi.fn(async () => true);
    fetchMock.mockResolvedValue(createJsonResponse(401, { message: 'Invalid credentials' }));

    const client = new HttpClient({
      baseUrl: 'http://api.test',
      onUnauthorized,
    });

    await expect(
      client.post('/auth/login', { email: 'user@example.com', password: 'invalid' }, { skipAuth: true }),
    ).rejects.toMatchObject({
      status: 401,
    });
    expect(onUnauthorized).not.toHaveBeenCalled();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
