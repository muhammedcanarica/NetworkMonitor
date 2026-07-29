import { lazy, Suspense } from 'react'
import { BrowserRouter, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/AppLayout'
import { DevicesPage } from './pages/DevicesPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { OverviewPage } from './pages/OverviewPage'
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
