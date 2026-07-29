import { useCallback, useEffect, useState } from 'react'

interface PollingState<T> {
  data: T | null
  error: string | null
  isLoading: boolean
  isRefreshing: boolean
  refresh: () => void
}

function errorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Veriler yüklenirken beklenmeyen bir hata oluştu.'
}

export function usePolling<T>(
  loader: (signal: AbortSignal) => Promise<T>,
  intervalMs = 5_000,
): PollingState<T> {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)
  useEffect(() => {
    let isActive = true
    let timeoutId: ReturnType<typeof setTimeout> | undefined
    let controller: AbortController | undefined

    const poll = async () => {
      controller = new AbortController()
      setIsRefreshing(true)

      try {
        const result = await loader(controller.signal)
        if (isActive) {
          setData(result)
          setError(null)
        }
      } catch (pollError) {
        if (
          isActive &&
          !(pollError instanceof DOMException && pollError.name === 'AbortError')
        ) {
          setError(errorMessage(pollError))
        }
      } finally {
        if (isActive) {
          setIsLoading(false)
          setIsRefreshing(false)
          timeoutId = setTimeout(poll, intervalMs)
        }
      }
    }

    void poll()

    return () => {
      isActive = false
      if (timeoutId) clearTimeout(timeoutId)
      controller?.abort()
    }
  }, [intervalMs, loader, refreshKey])

  const refresh = useCallback(() => setRefreshKey((key) => key + 1), [])

  return { data, error, isLoading, isRefreshing, refresh }
}
