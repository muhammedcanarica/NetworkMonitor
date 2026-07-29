import { apiRequest } from './client'
import type {
  SnmpConnectionRequest,
  SnmpInterface,
  SnmpSystemInfo,
  SnmpValue,
  SnmpWalkResponse,
} from '../types/api'

export const snmpApi = {
  systemInfo: (request: SnmpConnectionRequest, signal?: AbortSignal) =>
    apiRequest<SnmpSystemInfo>('/api/tools/snmp/system-info', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
  interfaces: (request: SnmpConnectionRequest, signal?: AbortSignal) =>
    apiRequest<SnmpInterface[]>('/api/tools/snmp/interfaces', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
  get: (request: SnmpConnectionRequest & { oid: string }, signal?: AbortSignal) =>
    apiRequest<SnmpValue>('/api/tools/snmp/get', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
  walk: (request: SnmpConnectionRequest & { rootOid: string }, signal?: AbortSignal) =>
    apiRequest<SnmpWalkResponse>('/api/tools/snmp/walk', {
      method: 'POST',
      body: JSON.stringify(request),
      signal,
    }),
}
