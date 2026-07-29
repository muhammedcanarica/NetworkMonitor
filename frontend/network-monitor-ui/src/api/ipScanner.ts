import { apiRequest } from './client'
import type { IpScanResponse } from '../types/api'

export const ipScannerApi = {
  scan: (cidr: string, signal?: AbortSignal) =>
    apiRequest<IpScanResponse>('/api/tools/ip-scan', {
      method: 'POST',
      body: JSON.stringify({ cidr }),
      signal,
    }),
}
