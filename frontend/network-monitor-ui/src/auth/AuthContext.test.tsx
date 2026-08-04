import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { authApi } from '../api/auth'
import { ProtectedLayout } from '../App'
import { AuthProvider } from './AuthContext'

vi.mock('../api/auth', () => ({
  authApi: { me: vi.fn(), login: vi.fn(), logout: vi.fn() },
}))
vi.mock('../notifications/NotificationProvider', () => ({ NotificationProvider: ({ children }: { children: React.ReactNode }) => children }))
vi.mock('../realtime/RealtimeProvider', () => ({ RealtimeProvider: ({ children }: { children: React.ReactNode }) => children }))
vi.mock('../components/notifications/NotificationCenter', () => ({ NotificationCenter: () => null }))
vi.mock('../components/realtime/ConnectionIndicator', () => ({ ConnectionIndicator: () => null }))

function renderProtected() {
  return render(
    <MemoryRouter initialEntries={['/protected']}>
      <AuthProvider>
        <Routes>
          <Route path="login" element={<div>Login page</div>} />
          <Route element={<ProtectedLayout />}>
            <Route path="protected" element={<div>Protected content</div>} />
          </Route>
        </Routes>
      </AuthProvider>
    </MemoryRouter>,
  )
}

describe('authentication routing', () => {
  beforeEach(() => vi.mocked(authApi.logout).mockResolvedValue())

  it('redirects an unauthenticated user to login', async () => {
    vi.mocked(authApi.me).mockRejectedValue(new Error('Unauthorized'))
    renderProtected()
    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
  })

  it('shows protected content to an authenticated user', async () => {
    vi.mocked(authApi.me).mockResolvedValue({ username: 'portfolio-admin' })
    renderProtected()
    expect(await screen.findByText('Protected content')).toBeInTheDocument()
  })

  it('hides protected content after logout', async () => {
    vi.mocked(authApi.me).mockResolvedValue({ username: 'portfolio-admin' })
    renderProtected()
    expect(await screen.findByText('Protected content')).toBeInTheDocument()
    await userEvent.click(screen.getByTitle('Sign out'))
    expect(await screen.findByText('Login page')).toBeInTheDocument()
    expect(screen.queryByText('Protected content')).not.toBeInTheDocument()
  })
})
