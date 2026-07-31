import { createContext } from 'react'

export interface NotificationContextValue {
  unreadCount: number
  unreadError: string | null
  refreshUnreadCount: () => Promise<void>
  markAsRead: (id: number) => Promise<void>
  markAllAsRead: () => Promise<void>
}

export const NotificationContext = createContext<NotificationContextValue | null>(null)
