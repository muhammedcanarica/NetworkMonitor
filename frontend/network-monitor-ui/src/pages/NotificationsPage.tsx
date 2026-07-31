import { useCallback, useEffect, useState } from 'react'
import { Bell, CheckCheck } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { notificationsApi } from '../api/notifications'
import { NotificationItem } from '../components/notifications/NotificationItem'
import { StatePanel } from '../components/ui/StatePanel'
import { useNotifications } from '../notifications/useNotifications'
import type { Notification } from '../types/api'

type Filter = 'All' | 'Unread'

export function NotificationsPage() {
  const [filter, setFilter] = useState<Filter>('All')
  const [items, setItems] = useState<Notification[] | null>(null)
  const [error, setError] = useState<string | null>(null)
  const { unreadCount, markAsRead, markAllAsRead } = useNotifications()
  const navigate = useNavigate()

  const load = useCallback((signal?: AbortSignal) => {
    setError(null)
    return notificationsApi.list(filter === 'Unread', 100, signal)
      .then(setItems)
      .catch((loadError) => {
        if (!(loadError instanceof DOMException && loadError.name === 'AbortError')) {
          setError(loadError instanceof Error ? loadError.message : 'Notifications could not be loaded.')
        }
      })
  }, [filter])

  useEffect(() => {
    setItems(null)
    const controller = new AbortController()
    void load(controller.signal)
    return () => controller.abort()
  }, [load])

  const openNotification = async (notification: Notification) => {
    if (!notification.isRead) {
      try { await markAsRead(notification.id) } catch { /* Do not block navigation on a read-state failure. */ }
    }
    navigate(notification.deviceId ? `/devices/${notification.deviceId}` : '/incidents')
  }
  const readNotification = async (notification: Notification) => {
    try {
      await markAsRead(notification.id)
      if (filter === 'Unread') setItems((current) => current?.filter((item) => item.id !== notification.id) ?? null)
      else setItems((current) => current?.map((item) => item.id === notification.id ? { ...item, isRead: true, readAt: new Date().toISOString() } : item) ?? null)
    } catch (readError) {
      setError(readError instanceof Error ? readError.message : 'Notification could not be marked as read.')
    }
  }
  const readAll = async () => {
    try {
      await markAllAsRead()
      if (filter === 'Unread') setItems([])
      else {
        const now = new Date().toISOString()
        setItems((current) => current?.map((item) => ({ ...item, isRead: true, readAt: item.readAt ?? now })) ?? null)
      }
    } catch (readError) {
      setError(readError instanceof Error ? readError.message : 'Notifications could not be marked as read.')
    }
  }

  return <div className="page notifications-page">
    <header className="page-header"><div><span className="eyebrow">Activity inbox</span><h1>Notifications</h1><p>In-app alerts created when monitoring incidents open.</p></div>{unreadCount > 0 && <button className="button secondary" type="button" onClick={() => void readAll()}><CheckCheck size={16} />Mark all as read</button>}</header>
    <section className="panel notifications-panel">
      <header className="panel-header"><div><h2>Notification history</h2><p>Newest 100 notifications. Read state is shared across this deployment.</p></div><Bell size={19} aria-hidden="true" /></header>
      <div className="port-result-filters" role="group" aria-label="Notification filters">
        {(['All', 'Unread'] as Filter[]).map((value) => <button key={value} type="button" className={filter === value ? 'active' : ''} onClick={() => setFilter(value)}>{value}</button>)}
      </div>
      {items === null && !error ? <StatePanel type="loading" title="Loading notifications" message="Reading the notification inbox…" />
        : error ? <StatePanel type="error" title="Notifications unavailable" message={error} action={<button className="button secondary" type="button" onClick={() => void load()}>Try again</button>} />
          : items?.length === 0 ? <StatePanel type="empty" title={filter === 'Unread' ? 'No unread notifications' : 'No notifications'} message={filter === 'Unread' ? 'All notifications have been read.' : 'New monitoring incidents will appear here.'} />
            : <div className="notification-page-list">{items?.map((item) => <NotificationItem key={item.id} notification={item} onOpen={(value) => void openNotification(value)} onMarkAsRead={(value) => void readNotification(value)} />)}</div>}
    </section>
  </div>
}
