export type DeviceStatus = 'Unknown' | 'Up' | 'Down' | 'Warning'

export interface Device {
  id: number
  name: string
  ipAddress: string
  description: string | null
  status: DeviceStatus
  lastSeenAt: string | null
  lastCheckedAt: string | null
  lastLatencyMs: number | null
  isMonitoringEnabled: boolean
  createdAt: string
  updatedAt: string
}

export interface CheckResult {
  id: number
  deviceId: number
  checkedAt: string
  isSuccess: boolean
  latencyMs: number | null
  deviceStatus: DeviceStatus
  failureReason: string | null
}

export interface DeviceSummary {
  totalChecks: number
  successfulChecks: number
  failedChecks: number
  uptimePercentage: number
  averageLatencyMs: number | null
  minLatencyMs: number | null
  maxLatencyMs: number | null
}

export interface DeviceMonitoringUpdate {
  deviceId: number
  status: DeviceStatus
  lastCheckedAt: string | null
  lastSeenAt: string | null
  lastLatencyMs: number | null
  isMonitoringEnabled: boolean
}

export interface CreateDeviceRequest {
  name: string
  ipAddress: string
  description: string | null
}

export interface UpdateDeviceRequest extends CreateDeviceRequest {
  isMonitoringEnabled: boolean
}

export interface IpScanHost {
  ipAddress: string
  isReachable: boolean
  latencyMs: number | null
  hostName: string | null
  isAlreadyMonitored: boolean
  deviceId: number | null
}

export interface IpScanResponse {
  cidr: string
  scannedAddresses: number
  reachableHosts: number
  durationMs: number
  results: IpScanHost[]
}

export interface WakeOnLanRequest {
  macAddress: string
  broadcastAddress: string
  port: number
}

export interface WakeOnLanResponse {
  macAddress: string
  broadcastAddress: string
  port: number
  message: string
}

export type PortState = 'Open' | 'Closed'

export interface PortScanRequest {
  ipAddress: string
  ports: number[]
  timeoutMilliseconds: number
}

export interface PortScanResult {
  port: number
  state: PortState
  latencyMs: number | null
  serviceName: string | null
}

export interface PortScanResponse {
  ipAddress: string
  scannedPorts: number
  openPorts: number
  durationMs: number
  results: PortScanResult[]
}

export interface SnmpConnectionRequest {
  ipAddress: string
  community: string
  timeoutMilliseconds: number
}

export interface SnmpValue {
  oid: string
  value: string | null
  type: string
}

export interface SnmpWalkResponse {
  rootOid: string
  count: number
  results: SnmpValue[]
}

export interface SnmpSystemInfo {
  ipAddress: string
  sysName: string | null
  sysDescription: string | null
  sysObjectId: string | null
  sysUpTimeTicks: number | null
  sysContact: string | null
  sysLocation: string | null
}

export interface SnmpInterface {
  index: number
  description: string | null
  adminStatus: 'Up' | 'Down' | 'Testing' | 'Unknown'
  operStatus: 'Up' | 'Down' | 'Testing' | 'Unknown'
  speedBitsPerSecond: number | null
}
