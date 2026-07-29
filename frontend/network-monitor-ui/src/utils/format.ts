const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
  dateStyle: 'medium',
  timeStyle: 'medium',
})

const timeFormatter = new Intl.DateTimeFormat(undefined, {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
})

export function formatLatency(value: number | null) {
  return value === null ? '—' : `${Math.round(value)} ms`
}

export function formatLocalDateTime(value: string | null) {
  return value ? dateTimeFormatter.format(new Date(value)) : 'Never'
}

export function formatTime(value: string) {
  return timeFormatter.format(new Date(value))
}

export function formatRelativeTime(value: string | null) {
  if (!value) return 'Never'

  const seconds = Math.round((new Date(value).getTime() - Date.now()) / 1_000)
  const absoluteSeconds = Math.abs(seconds)
  const formatter = new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' })

  if (absoluteSeconds < 60) return formatter.format(seconds, 'second')
  if (absoluteSeconds < 3_600)
    return formatter.format(Math.round(seconds / 60), 'minute')
  if (absoluteSeconds < 86_400)
    return formatter.format(Math.round(seconds / 3_600), 'hour')
  return formatter.format(Math.round(seconds / 86_400), 'day')
}

export function formatPercentage(value: number) {
  return `${value.toFixed(1)}%`
}
