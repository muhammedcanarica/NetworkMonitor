import { AlertTriangle, Inbox, LoaderCircle } from 'lucide-react'

interface StatePanelProps {
  type: 'loading' | 'error' | 'empty'
  title: string
  message: string
  action?: React.ReactNode
}

export function StatePanel({ type, title, message, action }: StatePanelProps) {
  const Icon = type === 'loading' ? LoaderCircle : type === 'error' ? AlertTriangle : Inbox

  return (
    <div className={`state-panel state-${type}`} role={type === 'error' ? 'alert' : undefined}>
      <Icon className={type === 'loading' ? 'spin' : ''} size={28} aria-hidden="true" />
      <div>
        <strong>{title}</strong>
        <p>{message}</p>
      </div>
      {action}
    </div>
  )
}
