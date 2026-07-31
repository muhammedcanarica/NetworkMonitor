import { apiRequest } from './client'
import type { EmailNotificationSettings, UpdateEmailNotificationSettingsRequest } from '../types/api'

export const emailNotificationsApi = {
  get: (signal?: AbortSignal) => apiRequest<EmailNotificationSettings>('/api/notification-settings/email', { signal }),
  update: (request: UpdateEmailNotificationSettingsRequest) => apiRequest<EmailNotificationSettings>('/api/notification-settings/email', { method: 'PUT', body: JSON.stringify(request) }),
  sendTest: () => apiRequest<{ message: string }>('/api/notification-settings/email/test', { method: 'POST' }),
}
