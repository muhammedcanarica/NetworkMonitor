import { apiRequest } from './client'
import type { TopologyDiscoveryRequest, TopologyDiscoveryResponse } from '../types/api'

export const topologyApi = {
  discover: (request: TopologyDiscoveryRequest, signal?: AbortSignal) =>
    apiRequest<TopologyDiscoveryResponse>('/api/topology/discover', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
}
