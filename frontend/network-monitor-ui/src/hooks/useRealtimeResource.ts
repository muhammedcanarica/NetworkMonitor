import { useCallback, useEffect, useState } from 'react'
import type { Dispatch, SetStateAction } from 'react'
import { useRealtimeConnection } from '../realtime/useRealtime'

interface RealtimeResourceState<T> {
  data: T | null
  setData: Dispatch<SetStateAction<T | null>>
  error: string | null
  isLoading: boolean
  isRefreshing: boolean
  refresh: () => void
}

const FALLBACK_POLL_INTERVAL_MS = 30_000

function errorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Veriler yüklenirken beklenmeyen bir hata oluştu.'
}

export function useRealtimeResource<T>(
  loader: (signal: AbortSignal) => Promise<T>,
): RealtimeResourceState<T> {
  const { status, syncVersion } = useRealtimeConnection()
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    let isActive = true
    const controller = new AbortController()

    const load = async () => {
      setIsRefreshing(true)

      try {
        const result = await loader(controller.signal)
        if (isActive) {
          setData(result)
          setError(null)
        }
      } catch (loadError) {
        if (
          isActive &&
          !(loadError instanceof DOMException && loadError.name === 'AbortError')
        ) {
          setError(errorMessage(loadError))
        }
      } finally {
        if (isActive) {
          setIsLoading(false)
          setIsRefreshing(false)
        }
      }
    }

    void load()

    return () => {
      isActive = false
      controller.abort()
    }
  }, [loader, refreshKey])

  useEffect(() => {
    if (syncVersion > 0) setRefreshKey((key) => key + 1)
  }, [syncVersion])

  useEffect(() => {
    if (status === 'connected') return

    const fallbackTimer = setInterval(
      () => setRefreshKey((key) => key + 1),
      FALLBACK_POLL_INTERVAL_MS,
    )
    return () => clearInterval(fallbackTimer)
  }, [status])

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), [])

  return { data, setData, error, isLoading, isRefreshing, refresh }
}
