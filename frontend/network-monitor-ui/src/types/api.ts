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
  username: string | null
  password: string | null
  credentialId: number | null
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
  community: string | null
  credentialId: number | null
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
  name: string | null
  description: string | null
  adminStatus: 'Up' | 'Down' | 'Testing' | 'Unknown'
  operStatus: 'Up' | 'Down' | 'Testing' | 'Unknown'
  speedBitsPerSecond: number | null
}

export interface SnmpMonitoredInterface {
  interfaceIndex: number
  interfaceName: string
  description: string | null
  isEnabled: boolean
}

export interface SnmpMonitoringProfile {
  deviceId: number
  credentialId: number
  isEnabled: boolean
  createdAt: string
  updatedAt: string
  interfaces: SnmpMonitoredInterface[]
}

export interface UpdateSnmpMonitoringRequest {
  credentialId: number
  isEnabled: boolean
  interfaceIndexes: number[]
}

export interface InterfaceTrafficSummary {
  interfaceIndex: number
  interfaceName: string
  description: string | null
  adminStatus: string | null
  operStatus: string | null
  lastSampleAt: string | null
  inboundBitsPerSecond: number | null
  outboundBitsPerSecond: number | null
  threshold: InterfaceBandwidthThreshold | null
  hasOpenInboundAlert: boolean
  hasOpenOutboundAlert: boolean
  hasActiveDownIncident: boolean
}

export interface InterfaceBandwidthThreshold {
  interfaceIndex: number
  inboundThresholdMbps: number | null
  outboundThresholdMbps: number | null
  breachSampleCount: number
  recoverySampleCount: number
  isEnabled: boolean
  createdAt: string
  updatedAt: string
}

export interface UpdateInterfaceBandwidthThresholdRequest {
  inboundThresholdMbps: number | null
  outboundThresholdMbps: number | null
  breachSampleCount: number
  recoverySampleCount: number
  isEnabled: boolean
}

export interface InterfaceTrafficSample {
  timestamp: string
  inOctets: number
  outOctets: number
  inboundBitsPerSecond: number | null
  outboundBitsPerSecond: number | null
  operStatus: string
}

export interface InterfaceTrafficHistory {
  interfaceIndex: number
  interfaceName: string
  hours: number
  samples: InterfaceTrafficSample[]
}

export interface TopologyDiscoveryRequest {
  deviceIds: number[]
  community: string | null
  credentialId: number | null
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

export type IncidentStatus = 'Open' | 'Resolved'
export type IncidentType = 'DeviceUnreachable' | 'InterfaceInboundBandwidthHigh' | 'InterfaceOutboundBandwidthHigh' | 'InterfaceDown'
export type BandwidthDirection = 'Inbound' | 'Outbound'

export interface Incident {
  id: number
  deviceId: number
  deviceName: string
  deviceIpAddress: string
  type: IncidentType
  status: IncidentStatus
  summary: string
  interfaceIndex: number | null
  interfaceName: string | null
  direction: BandwidthDirection | null
  thresholdBitsPerSecond: number | null
  observedBitsPerSecond: number | null
  startedAt: string
  resolvedAt: string | null
  durationSeconds: number
}

export type NotificationType = 'IncidentOpened'

export interface Notification {
  id: number
  type: NotificationType
  title: string
  message: string
  incidentId: number | null
  deviceId: number | null
  createdAt: string
  readAt: string | null
  isRead: boolean
}

export interface NotificationUnreadCount { count: number }

export type EmailTlsMode = 'None' | 'StartTls' | 'SslOnConnect'

export interface EmailNotificationSettings {
  isEnabled: boolean
  host: string
  port: number
  tlsMode: EmailTlsMode
  username: string | null
  fromAddress: string
  fromName: string | null
  recipientAddresses: string[]
  hasPassword: boolean
  updatedAt: string | null
}

export interface UpdateEmailNotificationSettingsRequest {
  isEnabled: boolean
  host: string
  port: number
  tlsMode: EmailTlsMode
  username: string | null
  password: string | null
  fromAddress: string
  fromName: string | null
  recipientAddresses: string[]
}

export interface CurrentUser { username: string }
export type NetworkCredentialType = 'SnmpV2Community' | 'SshPassword'
export interface NetworkCredential { id: number; name: string; type: NetworkCredentialType; username: string | null; deviceId: number | null; createdAt: string; updatedAt: string; hasSecret: boolean }
