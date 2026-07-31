import { useCallback, useEffect, useState } from 'react'
import { Bell, CheckCheck, LoaderCircle, X } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { notificationsApi } from '../../api/notifications'
import { useNotifications } from '../../notifications/useNotifications'
import type { Notification } from '../../types/api'
import { NotificationItem } from './NotificationItem'

export function NotificationCenter() {
  const [isOpen, setIsOpen] = useState(false)
  const [items, setItems] = useState<Notification[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { unreadCount, unreadError, markAsRead, markAllAsRead } = useNotifications()
  const navigate = useNavigate()
  const location = useLocation()

  const load = useCallback((signal?: AbortSignal) => {
    setError(null)
    return notificationsApi.list(false, 10, signal)
      .then(setItems)
      .catch((loadError) => {
        if (!(loadError instanceof DOMException && loadError.name === 'AbortError')) {
          setError(loadError instanceof Error ? loadError.message : 'Notifications could not be loaded.')
        }
      })
  }, [])

  useEffect(() => {
    if (!isOpen) return
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [isOpen, load])
  useEffect(() => setIsOpen(false), [location.pathname])

  const openNotification = async (notification: Notification) => {
    if (!notification.isRead) {
      try { await markAsRead(notification.id) } catch { /* Navigation remains available if the read update fails. */ }
    }
    navigate(notification.deviceId ? `/devices/${notification.deviceId}` : '/incidents')
  }
  const readNotification = async (notification: Notification) => {
    try {
      await markAsRead(notification.id)
      setItems((current) => current?.map((item) => item.id === notification.id ? { ...item, isRead: true, readAt: new Date().toISOString() } : item) ?? null)
    } catch (readError) {
      setError(readError instanceof Error ? readError.message : 'Notification could not be marked as read.')
    }
  }
  const readAll = async () => {
    try {
      await markAllAsRead()
      const now = new Date().toISOString()
      setItems((current) => current?.map((item) => ({ ...item, isRead: true, readAt: item.readAt ?? now })) ?? null)
    } catch (readError) {
      setError(readError instanceof Error ? readError.message : 'Notifications could not be marked as read.')
    }
  }

  return <div className="notification-center">
    <button className="notification-trigger" type="button" aria-haspopup="dialog" aria-expanded={isOpen} title={unreadError ?? 'Notifications'} onClick={() => setIsOpen((value) => !value)}>
      <Bell size={17} aria-hidden="true" /><span>Notifications</span>{unreadCount > 0 && <strong>{unreadCount > 99 ? '99+' : unreadCount}</strong>}
    </button>
    {isOpen && <section className="notification-drawer" role="dialog" aria-label="Notifications">
      <header><div><strong>Notifications</strong><small>{unreadCount} unread</small></div><div>{unreadCount > 0 && <button type="button" title="Mark all as read" onClick={() => void readAll()}><CheckCheck size={15} /></button>}<button type="button" title="Close" onClick={() => setIsOpen(false)}><X size={15} /></button></div></header>
      <div className="notification-drawer-list">
        {!items && !error ? <div className="notification-compact-state"><LoaderCircle className="spin" size={18} />Loading notifications…</div>
          : error ? <div className="notification-compact-state error"><span>{error}</span><button type="button" onClick={() => void load()}>Try again</button></div>
            : items?.length === 0 ? <div className="notification-compact-state">No notifications</div>
              : items?.map((item) => <NotificationItem compact key={item.id} notification={item} onOpen={(value) => void openNotification(value)} onMarkAsRead={(value) => void readNotification(value)} />)}
      </div>
      <Link className="notification-view-all" to="/notifications">View all notifications</Link>
    </section>}
  </div>
}
