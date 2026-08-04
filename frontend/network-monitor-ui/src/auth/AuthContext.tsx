import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import { authApi } from '../api/auth'
import type { CurrentUser } from '../types/api'

interface AuthValue { user: CurrentUser | null; loading: boolean; login: (username: string, password: string) => Promise<void>; logout: () => Promise<void> }
const AuthContext = createContext<AuthValue | null>(null)
export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(null); const [loading, setLoading] = useState(true)
  useEffect(() => { const controller = new AbortController(); authApi.me(controller.signal).then(setUser).catch(() => setUser(null)).finally(() => setLoading(false)); return () => controller.abort() }, [])
  const value = useMemo<AuthValue>(() => ({ user, loading, login: async (username, password) => setUser(await authApi.login(username, password)), logout: async () => { await authApi.logout(); setUser(null) } }), [loading, user])
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
// oxlint-disable-next-line react/only-export-components -- Keeping the hook beside its private context avoids exposing implementation details.
export function useAuth() { const value = useContext(AuthContext); if (!value) throw new Error('AuthProvider is missing.'); return value }
