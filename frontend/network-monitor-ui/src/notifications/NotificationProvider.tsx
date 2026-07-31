import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { notificationsApi } from '../api/notifications'
import { NotificationContext, type NotificationContextValue } from './notificationContext'

const REFRESH_INTERVAL_MS = 30_000

export function NotificationProvider({ children }: { children: ReactNode }) {
  const [unreadCount, setUnreadCount] = useState(0)
  const [unreadError, setUnreadError] = useState<string | null>(null)

  const refreshUnreadCount = useCallback(async () => {
    try {
      const response = await notificationsApi.unreadCount()
      setUnreadCount(response.count)
      setUnreadError(null)
    } catch (error) {
      setUnreadError(error instanceof Error ? error.message : 'Unread count could not be loaded.')
    }
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    notificationsApi.unreadCount(controller.signal)
      .then((response) => { setUnreadCount(response.count); setUnreadError(null) })
      .catch((error) => {
        if (!(error instanceof DOMException && error.name === 'AbortError')) {
          setUnreadError(error instanceof Error ? error.message : 'Unread count could not be loaded.')
        }
      })
    const interval = window.setInterval(() => void refreshUnreadCount(), REFRESH_INTERVAL_MS)
    return () => { controller.abort(); window.clearInterval(interval) }
  }, [refreshUnreadCount])

  const value = useMemo<NotificationContextValue>(() => ({
    unreadCount,
    unreadError,
    refreshUnreadCount,
    markAsRead: async (id) => {
      await notificationsApi.markAsRead(id)
      await refreshUnreadCount()
    },
    markAllAsRead: async () => {
      await notificationsApi.markAllAsRead()
      setUnreadCount(0)
      setUnreadError(null)
    },
  }), [refreshUnreadCount, unreadCount, unreadError])

  return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>
}
