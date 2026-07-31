import { apiRequest } from './client'
import type {
  InterfaceTrafficHistory,
  InterfaceTrafficSummary,
  SnmpInterface,
  SnmpMonitoringProfile,
  UpdateSnmpMonitoringRequest,
} from '../types/api'

export const snmpMonitoringApi = {
  get: (deviceId: number, signal?: AbortSignal) => apiRequest<SnmpMonitoringProfile | null>(`/api/devices/${deviceId}/snmp-monitoring`, { signal }),
  discoverInterfaces: (deviceId: number, credentialId: number, signal?: AbortSignal) => apiRequest<SnmpInterface[]>(`/api/devices/${deviceId}/snmp-monitoring/interfaces`, { method: 'POST', body: JSON.stringify({ credentialId, timeoutMilliseconds: 2000 }), signal }),
  update: (deviceId: number, request: UpdateSnmpMonitoringRequest) => apiRequest<SnmpMonitoringProfile>(`/api/devices/${deviceId}/snmp-monitoring`, { method: 'PUT', body: JSON.stringify(request) }),
  disable: (deviceId: number) => apiRequest<void>(`/api/devices/${deviceId}/snmp-monitoring`, { method: 'DELETE' }),
  summary: (deviceId: number, signal?: AbortSignal) => apiRequest<InterfaceTrafficSummary[]>(`/api/devices/${deviceId}/interface-traffic`, { signal }),
  history: (deviceId: number, interfaceIndex: number, hours: number, signal?: AbortSignal) => apiRequest<InterfaceTrafficHistory>(`/api/devices/${deviceId}/interfaces/${interfaceIndex}/traffic?hours=${hours}`, { signal }),
}
