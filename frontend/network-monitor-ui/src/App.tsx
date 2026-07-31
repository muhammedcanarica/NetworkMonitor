import { lazy, Suspense } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
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

const DeviceDetailPage = lazy(() =>
  import('./pages/DeviceDetailPage').then((module) => ({
    default: module.DeviceDetailPage,
  })),
)

function App() {
  return (
    <RealtimeProvider>
      <BrowserRouter>
        <Routes>
          <Route element={<AppLayout />}>
            <Route index element={<OverviewPage />} />
            <Route path="devices" element={<DevicesPage />} />
            <Route path="topology" element={<TopologyPage />} />
            <Route path="incidents" element={<IncidentsPage />} />
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
      </BrowserRouter>
    </RealtimeProvider>
  )
}

export default App
