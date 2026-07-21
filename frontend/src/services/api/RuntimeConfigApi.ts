import type { ApiResponse } from '../../types';
import type { AppRuntimeConfigurationDto } from '../../types/runtimeConfig';
import { httpClient, type HttpClient } from './HttpClient';

/**
 * Small API client for the public runtime configuration endpoint.
 */
export class RuntimeConfigApi {
  constructor(private readonly client: HttpClient = httpClient) {}

  getRuntimeConfiguration(): Promise<ApiResponse<AppRuntimeConfigurationDto>> {
    return this.client.get<ApiResponse<AppRuntimeConfigurationDto>>('/runtime-config', {
      skipAuth: true,
    });
  }
}

export const runtimeConfigApi = new RuntimeConfigApi();