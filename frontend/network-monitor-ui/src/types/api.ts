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

export type ConfigBackupVendor = 'CiscoIos'

export interface ConfigBackupRequest {
  ipAddress: string
  port: number
  username: string
  password: string
  vendor: ConfigBackupVendor
}

export interface ConfigBackupResponse {
  ipAddress: string
  vendor: ConfigBackupVendor
  configuration: string
  capturedAt: string
  suggestedFileName: string
}

export interface SaveConfigBackupRequest {
  deviceId: number | null
  ipAddress: string
  vendor: ConfigBackupVendor
  configuration: string
  capturedAt: string
}

export interface ConfigBackupListItem {
  id: number
  deviceId: number | null
  ipAddress: string
  vendor: ConfigBackupVendor
  capturedAt: string
  createdAt: string
  hash: string
  configurationLength: number
}

export interface ConfigBackupDetail extends ConfigBackupListItem {
  configuration: string
}

export interface SaveConfigBackupResponse {
  configurationChanged: boolean
  backupId: number
  existingBackupId: number | null
  backup: ConfigBackupListItem
}

export type ConfigDiffLineType = 'Added' | 'Removed' | 'Unchanged'

export interface ConfigDiffLine {
  type: ConfigDiffLineType
  fromLineNumber: number | null
  toLineNumber: number | null
  content: string
}

export interface ConfigBackupComparison {
  fromBackup: ConfigBackupListItem
  toBackup: ConfigBackupListItem
  addedLines: number
  removedLines: number
  changed: boolean
  diffLines: ConfigDiffLine[]
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

export interface TopologyDiscoveryRequest {
  deviceIds: number[]
  community: string
  timeoutMilliseconds: number
}

export interface TopologyNode {
  id: string
  deviceId: number | null
  ipAddress: string | null
  name: string
  status: DeviceStatus | null
  isManaged: boolean
}

export interface TopologyEdge {
  id: string
  sourceNodeId: string
  targetNodeId: string
  localPort: string | null
  remotePort: string | null
  discoveryProtocol: 'LLDP'
}

export interface TopologyDiscoveryResponse {
  nodes: TopologyNode[]
  edges: TopologyEdge[]
  scannedDevices: number
  successfulDevices: number
  failedDevices: number
  durationMs: number
  warnings: string[]
}
