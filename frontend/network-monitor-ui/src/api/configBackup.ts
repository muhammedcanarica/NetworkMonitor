import { apiRequest } from './client'
import type {
  ConfigBackupComparison,
  ConfigBackupDetail,
  ConfigBackupListItem,
  ConfigBackupRequest,
  ConfigBackupResponse,
  SaveConfigBackupRequest,
  SaveConfigBackupResponse,
} from '../types/api'

export const configBackupApi = {
  getRunningConfiguration: (request: ConfigBackupRequest, signal?: AbortSignal) =>
    apiRequest<ConfigBackupResponse>('/api/tools/config-backup', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
}

export const configBackupsApi = {
  save: (request: SaveConfigBackupRequest, signal?: AbortSignal) =>
    apiRequest<SaveConfigBackupResponse>('/api/config-backups', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
  list: (deviceId?: number, signal?: AbortSignal) =>
    apiRequest<ConfigBackupListItem[]>(
      deviceId ? `/api/config-backups/device/${deviceId}` : '/api/config-backups',
      { signal },
    ),
  get: (id: number, signal?: AbortSignal) =>
    apiRequest<ConfigBackupDetail>(`/api/config-backups/${id}`, { signal }),
  compare: (fromId: number, toId: number, signal?: AbortSignal) =>
    apiRequest<ConfigBackupComparison>(`/api/config-backups/compare?fromId=${fromId}&toId=${toId}`, { signal }),
}
