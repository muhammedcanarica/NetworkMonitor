import { useContext } from 'react'
import { NotificationContext } from './notificationContext'

export function useNotifications() {
  const value = useContext(NotificationContext)
  if (!value) throw new Error('NotificationProvider is missing.')
  return value
}
