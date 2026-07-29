import {
  Activity,
  Cable,
  DatabaseBackup,
  LayoutDashboard,
  Network,
  Power,
  Radar,
  Server,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { NavLink, Outlet } from 'react-router-dom'
import { ConnectionIndicator } from '../realtime/ConnectionIndicator'

interface ToolLink {
  label: string
  icon: LucideIcon
}

const futureTools: ToolLink[] = [
  { label: 'Wake-on-LAN', icon: Power },
  { label: 'Config Backup', icon: DatabaseBackup },
  { label: 'Port Scanner', icon: Cable },
]

export function AppLayout() {
  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">
            <Activity size={22} aria-hidden="true" />
          </span>
          <span>
            <strong>NetScope</strong>
            <small>Network Monitor</small>
          </span>
        </div>

        <nav className="primary-nav" aria-label="Primary navigation">
          <span className="nav-section-label">Monitor</span>
          <NavLink to="/" end>
            <LayoutDashboard size={18} aria-hidden="true" />
            Overview
          </NavLink>
          <NavLink to="/devices">
            <Server size={18} aria-hidden="true" />
            Devices
          </NavLink>

          <span className="nav-section-label tools-label">Tools</span>
          <NavLink to="/tools/ip-scanner">
            <Radar size={18} aria-hidden="true" />
            IP Scanner
          </NavLink>
          <NavLink to="/tools/snmp">
            <Network size={18} aria-hidden="true" />
            SNMP Explorer
          </NavLink>
          {futureTools.map(({ label, icon: Icon }) => (
            <span className="disabled-nav-item" key={label} aria-disabled="true">
              <Icon size={18} aria-hidden="true" />
              {label}
              <small>Soon</small>
            </span>
          ))}
        </nav>

        <div className="sidebar-footer">
          <ConnectionIndicator />
        </div>
      </aside>

      <main className="main-content">
        <Outlet />
      </main>
    </div>
  )
}
