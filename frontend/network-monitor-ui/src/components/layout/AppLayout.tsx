import {
  Activity,
  BellRing,
  Cable,
  DatabaseBackup,
  LayoutDashboard,
  Network,
  Power,
  Radar,
  Server,
  Share2,
  KeyRound,
  LogOut,
  Mail,
} from 'lucide-react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { ConnectionIndicator } from '../realtime/ConnectionIndicator'
import { useAuth } from '../../auth/AuthContext'
import { NotificationCenter } from '../notifications/NotificationCenter'

export function AppLayout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
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
          <NavLink to="/topology">
            <Share2 size={18} aria-hidden="true" />
            Topology
          </NavLink>
          <NavLink to="/incidents">
            <BellRing size={18} aria-hidden="true" />
            Incidents
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
          <NavLink to="/tools/wake-on-lan">
            <Power size={18} aria-hidden="true" />
            Wake-on-LAN
          </NavLink>
          <NavLink to="/tools/port-scanner">
            <Cable size={18} aria-hidden="true" />
            Port Scanner
          </NavLink>
          <NavLink to="/tools/config-backup">
            <DatabaseBackup size={18} aria-hidden="true" />
            Config Backup
          </NavLink>
          <span className="nav-section-label tools-label">Settings</span>
          <NavLink to="/settings/credentials"><KeyRound size={18} aria-hidden="true" />Credentials</NavLink>
          <NavLink to="/settings/notifications"><Mail size={18} aria-hidden="true" />Email Notifications</NavLink>
        </nav>

        <div className="sidebar-footer">
          <NotificationCenter />
          <ConnectionIndicator />
          <div className="sidebar-user"><span>{user?.username}</span><button type="button" title="Sign out" onClick={() => void logout().then(() => navigate('/login'))}><LogOut size={15} /></button></div>
        </div>
      </aside>

      <main className="main-content">
        <Outlet />
      </main>
    </div>
  )
}
