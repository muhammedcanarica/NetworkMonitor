import { apiRequest } from './client'
import type { ConfigBackupRequest, ConfigBackupResponse } from '../types/api'

export const configBackupApi = {
  getRunningConfiguration: (request: ConfigBackupRequest, signal?: AbortSignal) =>
    apiRequest<ConfigBackupResponse>('/api/tools/config-backup', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
}
