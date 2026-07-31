import { apiRequest } from './client'
import type { Incident, IncidentStatus } from '../types/api'

export const incidentsApi = {
  list: (status?: IncidentStatus, signal?: AbortSignal) =>
    apiRequest<Incident[]>(`/api/incidents${status ? `?status=${status}` : ''}`, { signal }),
  byDevice: (deviceId: number, signal?: AbortSignal) =>
    apiRequest<Incident[]>(`/api/incidents/device/${deviceId}`, { signal }),
}
