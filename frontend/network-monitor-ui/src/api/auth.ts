import { apiRequest, resetCsrfToken } from './client'
import type { CurrentUser } from '../types/api'

export const authApi = {
  me: (signal?: AbortSignal) => apiRequest<CurrentUser>('/api/auth/me', { signal }),
  login: async (username: string, password: string) => { const user = await apiRequest<CurrentUser>('/api/auth/login', { method: 'POST', body: JSON.stringify({ username, password }) }); resetCsrfToken(); return user },
  logout: async () => { await apiRequest<void>('/api/auth/logout', { method: 'POST' }); resetCsrfToken() },
}
