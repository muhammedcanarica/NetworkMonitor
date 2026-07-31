import { apiRequest } from './client'
import type { WakeOnLanRequest, WakeOnLanResponse } from '../types/api'

export const wakeOnLanApi = {
  send: (request: WakeOnLanRequest, signal?: AbortSignal) =>
    apiRequest<WakeOnLanResponse>('/api/tools/wake-on-lan', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
}
