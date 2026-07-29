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
