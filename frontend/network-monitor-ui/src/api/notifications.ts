import { apiRequest } from './client'
import type { Notification, NotificationUnreadCount } from '../types/api'

export const notificationsApi = {
  list: (unreadOnly = false, limit = 50, signal?: AbortSignal) =>
    apiRequest<Notification[]>(`/api/notifications?unreadOnly=${unreadOnly}&limit=${limit}`, { signal }),
  unreadCount: (signal?: AbortSignal) =>
    apiRequest<NotificationUnreadCount>('/api/notifications/unread-count', { signal }),
  markAsRead: (id: number) => apiRequest<void>(`/api/notifications/${id}/read`, { method: 'PUT' }),
  markAllAsRead: () => apiRequest<void>('/api/notifications/read-all', { method: 'PUT' }),
}
