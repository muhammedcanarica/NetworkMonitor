import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { notificationsApi } from '../../api/notifications'
import { NotificationContext, type NotificationContextValue } from '../../notifications/notificationContext'
import type { Notification } from '../../types/api'
import { NotificationCenter } from './NotificationCenter'

vi.mock('../../api/notifications', () => ({ notificationsApi: { list: vi.fn() } }))

const notification: Notification = {
  id: 7, type: 'IncidentOpened', title: 'High traffic', message: 'Threshold exceeded',
  incidentId: 4, deviceId: 2, createdAt: '2026-08-04T08:00:00Z', readAt: null, isRead: false,
}

function renderCenter(overrides: Partial<NotificationContextValue> = {}) {
  const value: NotificationContextValue = {
    unreadCount: 3,
    unreadError: null,
    refreshUnreadCount: vi.fn().mockResolvedValue(undefined),
    markAsRead: vi.fn().mockResolvedValue(undefined),
    markAllAsRead: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  }
  render(<MemoryRouter><NotificationContext.Provider value={value}><NotificationCenter /></NotificationContext.Provider></MemoryRouter>)
  return value
}

describe('NotificationCenter', () => {
  beforeEach(() => vi.mocked(notificationsApi.list).mockResolvedValue([notification]))

  it('shows the unread badge and opens the notification list', async () => {
    renderCenter()
    expect(screen.getByText('3')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /notifications/i }))
    expect(await screen.findByText('High traffic')).toBeInTheDocument()
  })

  it('shows the empty state', async () => {
    vi.mocked(notificationsApi.list).mockResolvedValue([])
    renderCenter({ unreadCount: 0 })
    await userEvent.click(screen.getByRole('button', { name: /notifications/i }))
    expect(await screen.findByText('No notifications')).toBeInTheDocument()
  })

  it('marks one notification as read', async () => {
    const markAsRead = vi.fn().mockResolvedValue(undefined)
    renderCenter({ markAsRead })
    await userEvent.click(screen.getByRole('button', { name: /notifications/i }))
    await screen.findByText('High traffic')
    await userEvent.click(screen.getByTitle('Mark as read'))
    await waitFor(() => expect(markAsRead).toHaveBeenCalledWith(7))
  })

  it('marks every notification as read', async () => {
    const markAllAsRead = vi.fn().mockResolvedValue(undefined)
    renderCenter({ markAllAsRead })
    await userEvent.click(screen.getByRole('button', { name: /notifications/i }))
    await userEvent.click(screen.getByTitle('Mark all as read'))
    await waitFor(() => expect(markAllAsRead).toHaveBeenCalledOnce())
  })
})
