import { apiRequest } from './client'
import type {
  CheckResult,
  CreateDeviceRequest,
  Device,
  DeviceSummary,
  UpdateDeviceRequest,
} from '../types/api'

export const devicesApi = {
  list: (signal?: AbortSignal) =>
    apiRequest<Device[]>('/api/devices', { signal }),
  get: (id: number, signal?: AbortSignal) =>
    apiRequest<Device>(`/api/devices/${id}`, { signal }),
  create: (request: CreateDeviceRequest) =>
    apiRequest<Device>('/api/devices', {
      method: 'POST',
      body: JSON.stringify(request),
    }),
  update: (id: number, request: UpdateDeviceRequest) =>
    apiRequest<Device>(`/api/devices/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    }),
  remove: (id: number) =>
    apiRequest<void>(`/api/devices/${id}`, { method: 'DELETE' }),
  checks: (id: number, limit = 100, signal?: AbortSignal) =>
    apiRequest<CheckResult[]>(`/api/devices/${id}/checks?limit=${limit}`, {
      signal,
    }),
  summary: (id: number, signal?: AbortSignal) =>
    apiRequest<DeviceSummary>(`/api/devices/${id}/summary`, { signal }),
}
