import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { AppLayout } from './components/layout/AppLayout'
import { DevicesPage } from './pages/DevicesPage'
import { ConfigBackupPage } from './pages/ConfigBackupPage'
import { ConfigBackupHistoryPage } from './pages/ConfigBackupHistoryPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { OverviewPage } from './pages/OverviewPage'
import { IpScannerPage } from './pages/IpScannerPage'
import { PortScannerPage } from './pages/PortScannerPage'
import { SnmpExplorerPage } from './pages/SnmpExplorerPage'
import { WakeOnLanPage } from './pages/WakeOnLanPage'
import { TopologyPage } from './pages/TopologyPage'
import { IncidentsPage } from './pages/IncidentsPage'
import { RealtimeProvider } from './realtime/RealtimeProvider'
import './App.css'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { LoginPage } from './pages/LoginPage'
import { CredentialsPage } from './pages/CredentialsPage'

const DeviceDetailPage = lazy(() =>
  import('./pages/DeviceDetailPage').then((module) => ({
    default: module.DeviceDetailPage,
  })),
)

function App() {
  return (
    <BrowserRouter><AuthProvider>
        <Routes>
          <Route path="login" element={<LoginPage />} />
          <Route element={<ProtectedLayout />}>
            <Route index element={<OverviewPage />} />
            <Route path="devices" element={<DevicesPage />} />
            <Route path="topology" element={<TopologyPage />} />
            <Route path="incidents" element={<IncidentsPage />} />
            <Route path="settings/credentials" element={<CredentialsPage />} />
            <Route path="tools/ip-scanner" element={<IpScannerPage />} />
            <Route path="tools/config-backup" element={<ConfigBackupPage />} />
            <Route path="tools/config-backup/history" element={<ConfigBackupHistoryPage />} />
            <Route path="tools/port-scanner" element={<PortScannerPage />} />
            <Route path="tools/wake-on-lan" element={<WakeOnLanPage />} />
            <Route path="tools/snmp" element={<SnmpExplorerPage />} />
            <Route
              path="devices/:id"
              element={
                <Suspense fallback={<div className="route-loading">Loading device details…</div>}>
                  <DeviceDetailPage />
                </Suspense>
              }
            />
            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
    </AuthProvider></BrowserRouter>
  )
}

function ProtectedLayout() {
  const { user, loading } = useAuth(); const location = useLocation()
  if (loading) return <div className="route-loading">Checking authentication…</div>
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  return <RealtimeProvider><AppLayout /></RealtimeProvider>
}

export default App
