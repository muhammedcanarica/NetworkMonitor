import {
  HubConnectionBuilder,
  HubConnectionState,
} from '@microsoft/signalr'
import { API_BASE_URL } from '../api/client'
import type { DeviceMonitoringUpdate } from '../types/api'

export type MonitoringConnectionStatus =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting'

type StatusListener = (status: MonitoringConnectionStatus) => void
type UpdateListener = (update: DeviceMonitoringUpdate) => void
type SyncListener = () => void

const DEVICE_UPDATED_EVENT = 'DeviceMonitoringUpdated'
const RETRY_DELAY_MS = 5_000

class MonitoringConnectionManager {
  private readonly connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/monitoring`, { withCredentials: true })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
    .build()

  private readonly statusListeners = new Set<StatusListener>()
  private readonly updateListeners = new Set<UpdateListener>()
  private readonly syncListeners = new Set<SyncListener>()
  private status: MonitoringConnectionStatus = 'disconnected'
  private startPromise: Promise<void> | null = null
  private retryTimer: ReturnType<typeof setTimeout> | null = null

  constructor() {
    this.connection.on(
      DEVICE_UPDATED_EVENT,
      (update: DeviceMonitoringUpdate) => {
        this.updateListeners.forEach((listener) => listener(update))
      },
    )

    this.connection.onreconnecting(() => {
      this.setStatus('reconnecting')
    })

    this.connection.onreconnected(() => {
      this.setStatus('connected')
      this.notifySyncRequired()
    })

    this.connection.onclose(() => {
      this.setStatus('disconnected')
      this.scheduleRetry()
    })
  }

  start = () => {
    if (
      this.connection.state !== HubConnectionState.Disconnected ||
      this.startPromise
    ) {
      return this.startPromise ?? Promise.resolve()
    }

    if (this.retryTimer) {
      clearTimeout(this.retryTimer)
      this.retryTimer = null
    }

    this.setStatus('connecting')
    const startAttempt = this.connection
      .start()
      .then(() => {
        this.setStatus('connected')
        this.notifySyncRequired()
      })
      .catch(() => {
        this.setStatus('disconnected')
        this.scheduleRetry()
      })
      .finally(() => {
        if (this.startPromise === startAttempt) this.startPromise = null
      })

    this.startPromise = startAttempt
    return startAttempt
  }

  subscribeToStatus = (listener: StatusListener) => {
    this.statusListeners.add(listener)
    listener(this.status)
    return () => {
      this.statusListeners.delete(listener)
    }
  }

  subscribeToUpdates = (listener: UpdateListener) => {
    this.updateListeners.add(listener)
    return () => {
      this.updateListeners.delete(listener)
    }
  }

  subscribeToSync = (listener: SyncListener) => {
    this.syncListeners.add(listener)
    return () => {
      this.syncListeners.delete(listener)
    }
  }

  private setStatus(nextStatus: MonitoringConnectionStatus) {
    if (this.status === nextStatus) return
    this.status = nextStatus
    this.statusListeners.forEach((listener) => listener(nextStatus))
  }

  private notifySyncRequired() {
    this.syncListeners.forEach((listener) => listener())
  }

  private scheduleRetry() {
    if (this.retryTimer) return
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null
      void this.start()
    }, RETRY_DELAY_MS)
  }
}

export const monitoringConnection = new MonitoringConnectionManager()
