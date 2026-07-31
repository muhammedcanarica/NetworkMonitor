import { apiRequest } from './client'
import type { PortScanRequest, PortScanResponse } from '../types/api'

export const portScannerApi = {
  scan: (request: PortScanRequest, signal?: AbortSignal) =>
    apiRequest<PortScanResponse>('/api/tools/port-scanner', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
}
