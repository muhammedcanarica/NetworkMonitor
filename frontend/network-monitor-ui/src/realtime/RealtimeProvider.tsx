import {
  useEffect,
  useMemo,
  useState,
} from 'react'
import type { ReactNode } from 'react'
import {
  monitoringConnection,
  type MonitoringConnectionStatus,
} from './monitoringConnection'
import { RealtimeContext, type RealtimeContextValue } from './realtimeContext'

export function RealtimeProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<MonitoringConnectionStatus>('disconnected')
  const [syncVersion, setSyncVersion] = useState(0)

  useEffect(() => {
    const unsubscribeStatus = monitoringConnection.subscribeToStatus(setStatus)
    const unsubscribeSync = monitoringConnection.subscribeToSync(() => {
      setSyncVersion((version) => version + 1)
    })

    void monitoringConnection.start()

    return () => {
      unsubscribeStatus()
      unsubscribeSync()
    }
  }, [])

  const value = useMemo<RealtimeContextValue>(
    () => ({
      status,
      syncVersion,
      subscribeToUpdates: monitoringConnection.subscribeToUpdates,
    }),
    [status, syncVersion],
  )

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>
}
