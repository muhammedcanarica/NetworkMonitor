import { Component, type ErrorInfo, type ReactNode } from 'react'
import { StatePanel } from '../ui/StatePanel'

interface DevicePanelErrorBoundaryProps {
  children: ReactNode
  panelName: string
}

interface DevicePanelErrorBoundaryState {
  hasError: boolean
}

export class DevicePanelErrorBoundary extends Component<DevicePanelErrorBoundaryProps, DevicePanelErrorBoundaryState> {
  public state: DevicePanelErrorBoundaryState = { hasError: false }

  public static getDerivedStateFromError(): DevicePanelErrorBoundaryState {
    return { hasError: true }
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error(`${this.props.panelName} panel failed to render.`, error, errorInfo)
  }

  public render() {
    if (this.state.hasError) {
      return <section className="panel"><StatePanel type="error" title={`${this.props.panelName} unavailable`} message="This panel could not be displayed. Other device details are still available." /></section>
    }

    return this.props.children
  }
}
