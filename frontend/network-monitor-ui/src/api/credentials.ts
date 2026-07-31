import { apiRequest } from './client'
import type { NetworkCredential, NetworkCredentialType } from '../types/api'
export interface CredentialRequest { name: string; type: NetworkCredentialType; username: string | null; secret?: string; deviceId: number | null }
export const credentialsApi = {
  list: (signal?: AbortSignal) => apiRequest<NetworkCredential[]>('/api/network-credentials', { signal }),
  create: (request: CredentialRequest) => apiRequest<NetworkCredential>('/api/network-credentials', { method: 'POST', body: JSON.stringify(request) }),
  update: (id: number, request: CredentialRequest) => apiRequest<NetworkCredential>(`/api/network-credentials/${id}`, { method: 'PUT', body: JSON.stringify(request) }),
  remove: (id: number) => apiRequest<void>(`/api/network-credentials/${id}`, { method: 'DELETE' }),
}
