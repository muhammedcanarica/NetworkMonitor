import { createContext } from 'react'
import type { DeviceMonitoringUpdate } from '../types/api'
import type { MonitoringConnectionStatus } from './monitoringConnection'

export interface RealtimeContextValue {
  status: MonitoringConnectionStatus
  syncVersion: number
  subscribeToUpdates: (
    listener: (update: DeviceMonitoringUpdate) => void,
  ) => () => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
