import type { ApiError, ApiResponse } from '../../types';
import { tokenManager } from './TokenManager';
import { emitApiNotice } from './apiEvents';

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE';

export class HttpError extends Error {
  public readonly status: number;
  public readonly payload: unknown;

  constructor(status: number, message: string, payload: unknown) {
    super(message);
    this.name = 'HttpError';
    this.status = status;
    this.payload = payload;
  }
}

export interface HttpClientOptions {
  baseUrl?: string;
  onUnauthorized?: () => Promise<boolean>;
}

export interface RequestOptions<TBody> {
  body?: TBody;
  headers?: HeadersInit;
  skipAuth?: boolean;
  signal?: AbortSignal;
}

export class HttpClient {
  private readonly baseUrl: string;
  private onUnauthorized?: () => Promise<boolean>;

  constructor(options: HttpClientOptions = {}) {
    const configuredBaseUrl = options.baseUrl ?? import.meta.env.VITE_API_URL ?? 'http://localhost:5000';
    this.baseUrl = configuredBaseUrl.endsWith('/api')
      ? configuredBaseUrl.replace(/\/$/, '')
      : `${configuredBaseUrl.replace(/\/$/, '')}/api`;
    this.onUnauthorized = options.onUnauthorized;
  }

  async get<TResponse>(path: string, options: Omit<RequestOptions<never>, 'body'> = {}): Promise<TResponse> {
    return this.request<TResponse, never>('GET', path, options);
  }

  async getBlob(path: string, options: Omit<RequestOptions<never>, 'body'> = {}): Promise<Blob> {
    return this.requestBlob(path, options);
  }

  async post<TResponse, TBody = unknown>(path: string, body?: TBody, options: Omit<RequestOptions<TBody>, 'body'> = {}): Promise<TResponse> {
    return this.request<TResponse, TBody>('POST', path, { ...options, body });
  }

  async put<TResponse, TBody = unknown>(path: string, body?: TBody, options: Omit<RequestOptions<TBody>, 'body'> = {}): Promise<TResponse> {
    return this.request<TResponse, TBody>('PUT', path, { ...options, body });
  }

  async patch<TResponse, TBody = unknown>(path: string, body?: TBody, options: Omit<RequestOptions<TBody>, 'body'> = {}): Promise<TResponse> {
    return this.request<TResponse, TBody>('PATCH', path, { ...options, body });
  }

  async delete<TResponse, TBody = unknown>(path: string, body?: TBody, options: Omit<RequestOptions<TBody>, 'body'> = {}): Promise<TResponse> {
    return this.request<TResponse, TBody>('DELETE', path, { ...options, body });
  }

  setUnauthorizedHandler(handler?: () => Promise<boolean>) {
    this.onUnauthorized = handler;
  }

  private async request<TResponse, TBody>(
    method: HttpMethod,
    path: string,
    options: RequestOptions<TBody> = {},
    retried = false,
  ): Promise<TResponse> {
    const url = `${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
    const headers = new Headers(options.headers);
    const isFormDataBody = typeof FormData !== 'undefined' && options.body instanceof FormData;
    headers.set('Accept', 'application/json');
    if (options.body && !isFormDataBody) {
      headers.set('Content-Type', 'application/json');
    }

    if (!options.skipAuth) {
      const accessToken = tokenManager.getAccessToken();
      if (accessToken) {
        headers.set('Authorization', `Bearer ${accessToken}`);
      }
    }

    const response = await fetch(url, {
      method,
      headers,
      credentials: 'include',
      signal: options.signal,
      body: options.body ? isFormDataBody ? options.body as BodyInit : JSON.stringify(options.body) : undefined,
    });

    const parsed = await this.parseResponse<TResponse>(response);

    if (response.ok) {
      return parsed;
    }

    if (response.status === 401 && this.onUnauthorized && !retried) {
      const refreshed = await this.onUnauthorized();
      if (refreshed) {
        return this.request<TResponse, TBody>(method, path, options, true);
      }
    }

    const apiError = parsed as ApiResponse<unknown> | ApiError | null;
    const message = this.resolveErrorMessage(apiError, response.statusText);

    if (!options.skipAuth && response.status === 401) {
      emitApiNotice({
        code: 'session-expired',
        message: 'Your session expired. Please sign in again.',
      });
    }

    if (!options.skipAuth && response.status === 403) {
      emitApiNotice({
        code: 'forbidden',
        message: message || 'You do not have permission to perform this action.',
      });
    }

    throw new HttpError(response.status, message, apiError);
  }

  private async requestBlob(
    path: string,
    options: Omit<RequestOptions<never>, 'body'> = {},
    retried = false,
  ): Promise<Blob> {
    const url = `${this.baseUrl}${path.startsWith('/') ? path : `/${path}`}`;
    const headers = new Headers(options.headers);
    headers.set('Accept', 'application/octet-stream');

    if (!options.skipAuth) {
      const accessToken = tokenManager.getAccessToken();
      if (accessToken) {
        headers.set('Authorization', `Bearer ${accessToken}`);
      }
    }

    const response = await fetch(url, {
      method: 'GET',
      headers,
      credentials: 'include',
      signal: options.signal,
    });

    if (response.ok) {
      return response.blob();
    }

    const parsed = await this.parseResponse<unknown>(response);
    if (response.status === 401 && this.onUnauthorized && !retried) {
      const refreshed = await this.onUnauthorized();
      if (refreshed) {
        return this.requestBlob(path, options, true);
      }
    }

    const apiError = parsed as ApiResponse<unknown> | ApiError | null;
    const message = this.resolveErrorMessage(apiError, response.statusText);
    if (!options.skipAuth && response.status === 401) {
      emitApiNotice({ code: 'session-expired', message: 'Your session expired. Please sign in again.' });
    }
    if (!options.skipAuth && response.status === 403) {
      emitApiNotice({ code: 'forbidden', message: message || 'You do not have permission to perform this action.' });
    }

    throw new HttpError(response.status, message, apiError);
  }

  private async parseResponse<TResponse>(response: Response): Promise<TResponse> {
    if (response.status === 204) {
      return undefined as TResponse;
    }

    const contentType = response.headers.get('content-type') ?? '';
    if (contentType.includes('application/json')) {
      return (await response.json()) as TResponse;
    }

    const text = await response.text();
    return text as unknown as TResponse;
  }

  private resolveErrorMessage(payload: unknown, fallback: string): string {
    if (payload && typeof payload === 'object' && 'message' in payload) {
      const message = (payload as { message?: unknown }).message;
      if (typeof message === 'string' && message.trim()) {
        return message;
      }
    }

    return fallback || 'Request failed';
  }
}

export const httpClient = new HttpClient();