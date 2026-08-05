import { apiRequest } from './client'
import type {
  InterfaceTrafficHistory,
  InterfaceTrafficSummary,
  SnmpInterface,
  SnmpMonitoringProfile,
  UpdateSnmpMonitoringRequest,
  InterfaceBandwidthThreshold,
  UpdateInterfaceBandwidthThresholdRequest,
} from '../types/api'

export const snmpMonitoringApi = {
  get: async (deviceId: number, signal?: AbortSignal) =>
    (await apiRequest<SnmpMonitoringProfile | null | undefined>(`/api/devices/${deviceId}/snmp-monitoring`, { signal })) ?? null,
  discoverInterfaces: (deviceId: number, credentialId: number, signal?: AbortSignal) => apiRequest<SnmpInterface[]>(`/api/devices/${deviceId}/snmp-monitoring/interfaces`, { method: 'POST', body: JSON.stringify({ credentialId, timeoutMilliseconds: 2000 }), signal }),
  update: (deviceId: number, request: UpdateSnmpMonitoringRequest) => apiRequest<SnmpMonitoringProfile>(`/api/devices/${deviceId}/snmp-monitoring`, { method: 'PUT', body: JSON.stringify(request) }),
  disable: (deviceId: number) => apiRequest<void>(`/api/devices/${deviceId}/snmp-monitoring`, { method: 'DELETE' }),
  summary: (deviceId: number, signal?: AbortSignal) => apiRequest<InterfaceTrafficSummary[]>(`/api/devices/${deviceId}/interface-traffic`, { signal }),
  history: (deviceId: number, interfaceIndex: number, hours: number, signal?: AbortSignal) => apiRequest<InterfaceTrafficHistory>(`/api/devices/${deviceId}/interfaces/${interfaceIndex}/traffic?hours=${hours}`, { signal }),
  getThreshold: (deviceId: number, interfaceIndex: number, signal?: AbortSignal) => apiRequest<InterfaceBandwidthThreshold | null>(`/api/devices/${deviceId}/interfaces/${interfaceIndex}/bandwidth-threshold`, { signal }),
  updateThreshold: (deviceId: number, interfaceIndex: number, request: UpdateInterfaceBandwidthThresholdRequest) => apiRequest<InterfaceBandwidthThreshold>(`/api/devices/${deviceId}/interfaces/${interfaceIndex}/bandwidth-threshold`, { method: 'PUT', body: JSON.stringify(request) }),
  deleteThreshold: (deviceId: number, interfaceIndex: number) => apiRequest<void>(`/api/devices/${deviceId}/interfaces/${interfaceIndex}/bandwidth-threshold`, { method: 'DELETE' }),
}
