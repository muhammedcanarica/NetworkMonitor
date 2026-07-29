import { ArrowLeft, SearchX } from 'lucide-react'
import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="not-found">
      <SearchX size={42} aria-hidden="true" />
      <span className="eyebrow">404</span>
      <h1>Page not found</h1>
      <p>The requested dashboard page does not exist.</p>
      <Link className="button primary" to="/"><ArrowLeft size={16} /> Return to overview</Link>
    </div>
  )
}
