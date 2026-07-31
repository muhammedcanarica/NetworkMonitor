import { Check, CircleAlert } from 'lucide-react'
import type { Notification } from '../../types/api'
import { formatRelativeTime } from '../../utils/format'

interface NotificationItemProps {
  notification: Notification
  onOpen: (notification: Notification) => void
  onMarkAsRead: (notification: Notification) => void
  compact?: boolean
}

export function NotificationItem({ notification, onOpen, onMarkAsRead, compact = false }: NotificationItemProps) {
  return <article className={`notification-item ${notification.isRead ? 'is-read' : 'is-unread'} ${compact ? 'compact' : ''}`}>
    <button className="notification-main" type="button" onClick={() => onOpen(notification)}>
      <span className="notification-icon"><CircleAlert size={16} aria-hidden="true" /></span>
      <span className="notification-copy"><strong>{notification.title}</strong><span>{notification.message}</span><small title={new Date(notification.createdAt).toLocaleString()}>{formatRelativeTime(notification.createdAt)}</small></span>
      {!notification.isRead && <span className="notification-unread-dot" title="Unread" />}
    </button>
    {!notification.isRead && <button className="notification-read-button" type="button" title="Mark as read" aria-label={`Mark ${notification.title} as read`} onClick={() => onMarkAsRead(notification)}><Check size={14} /></button>}
  </article>
}
