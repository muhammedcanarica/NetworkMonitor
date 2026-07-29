import { useContext, useEffect, useRef } from 'react'
import type { DeviceMonitoringUpdate } from '../types/api'
import { RealtimeContext } from './realtimeContext'

export function useRealtimeConnection() {
  const context = useContext(RealtimeContext)
  if (!context) {
    throw new Error('useRealtimeConnection must be used inside RealtimeProvider.')
  }
  return context
}

export function useMonitoringUpdates(
  handler: (update: DeviceMonitoringUpdate) => void,
) {
  const { subscribeToUpdates } = useRealtimeConnection()
  const handlerRef = useRef(handler)

  useEffect(() => {
    handlerRef.current = handler
  }, [handler])

  useEffect(
    () => subscribeToUpdates((update) => handlerRef.current(update)),
    [subscribeToUpdates],
  )
}
