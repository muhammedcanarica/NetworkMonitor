import type { LucideIcon } from 'lucide-react'

interface MetricCardProps {
  label: string
  value: string | number
  hint?: string
  icon: LucideIcon
  tone?: 'neutral' | 'up' | 'warning' | 'down'
}

export function MetricCard({
  label,
  value,
  hint,
  icon: Icon,
  tone = 'neutral',
}: MetricCardProps) {
  return (
    <article className={`metric-card metric-${tone}`}>
      <div className="metric-icon">
        <Icon size={20} aria-hidden="true" />
      </div>
      <div>
        <span className="metric-label">{label}</span>
        <strong>{value}</strong>
        {hint && <small>{hint}</small>}
      </div>
    </article>
  )
}
