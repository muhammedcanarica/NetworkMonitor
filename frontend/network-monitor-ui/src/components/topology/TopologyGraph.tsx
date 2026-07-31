import { useMemo, useRef, useState } from 'react'
import type { TopologyEdge, TopologyNode } from '../../types/api'

interface TopologyGraphProps {
  nodes: TopologyNode[]
  edges: TopologyEdge[]
  onManagedNodeClick: (deviceId: number) => void
}

interface Position {
  x: number
  y: number
}

const GRAPH_WIDTH = 1000
const GRAPH_HEIGHT = 560

export function TopologyGraph({ nodes, edges, onManagedNodeClick }: TopologyGraphProps) {
  const [zoom, setZoom] = useState(1)
  const [pan, setPan] = useState<Position>({ x: 0, y: 0 })
  const dragStart = useRef<Position | null>(null)
  const positions = useMemo(() => createLayout(nodes), [nodes])

  const handleWheel = (event: React.WheelEvent<SVGSVGElement>) => {
    event.preventDefault()
    setZoom((current) => Math.min(1.8, Math.max(0.55, current + (event.deltaY < 0 ? 0.1 : -0.1))))
  }

  const startDrag = (event: React.PointerEvent<SVGSVGElement>) => {
    if (event.target !== event.currentTarget) return
    dragStart.current = { x: event.clientX - pan.x, y: event.clientY - pan.y }
    event.currentTarget.setPointerCapture(event.pointerId)
  }

  const moveDrag = (event: React.PointerEvent<SVGSVGElement>) => {
    if (!dragStart.current) return
    setPan({ x: event.clientX - dragStart.current.x, y: event.clientY - dragStart.current.y })
  }

  const endDrag = () => { dragStart.current = null }

  return (
    <div className="topology-graph-shell">
      <div className="topology-graph-toolbar">
        <span>Drag to pan</span>
        <div>
          <button type="button" onClick={() => setZoom((value) => Math.max(0.55, value - 0.15))} aria-label="Zoom out">−</button>
          <button type="button" onClick={() => { setZoom(1); setPan({ x: 0, y: 0 }) }}>Fit</button>
          <button type="button" onClick={() => setZoom((value) => Math.min(1.8, value + 0.15))} aria-label="Zoom in">+</button>
        </div>
      </div>
      <svg
        className="topology-graph"
        viewBox={`0 0 ${GRAPH_WIDTH} ${GRAPH_HEIGHT}`}
        role="img"
        aria-label="LLDP network topology graph"
        onWheel={handleWheel}
        onPointerDown={startDrag}
        onPointerMove={moveDrag}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
      >
        <defs>
          <pattern id="topology-grid" width="26" height="26" patternUnits="userSpaceOnUse">
            <path d="M 26 0 L 0 0 0 26" fill="none" stroke="rgba(122, 147, 174, 0.12)" strokeWidth="1" />
          </pattern>
        </defs>
        <rect width={GRAPH_WIDTH} height={GRAPH_HEIGHT} fill="url(#topology-grid)" />
        <g transform={`translate(${pan.x} ${pan.y}) scale(${zoom})`}>
          {edges.map((edge) => {
            const source = positions.get(edge.sourceNodeId)
            const target = positions.get(edge.targetNodeId)
            if (!source || !target) return null
            const label = edge.localPort && edge.remotePort ? `${edge.localPort} ↔ ${edge.remotePort}` : edge.localPort ?? edge.remotePort ?? 'LLDP'
            return (
              <g key={edge.id} className="topology-edge">
                <line x1={source.x} y1={source.y} x2={target.x} y2={target.y} />
                <text x={(source.x + target.x) / 2} y={(source.y + target.y) / 2 - 8}>{label}</text>
              </g>
            )
          })}
          {nodes.map((node) => {
            const position = positions.get(node.id)
            if (!position) return null
            const isClickable = node.isManaged && node.deviceId !== null
            return (
              <g
                key={node.id}
                className={`topology-node ${node.isManaged ? 'managed' : 'discovered'} ${isClickable ? 'clickable' : ''}`}
                transform={`translate(${position.x} ${position.y})`}
                onClick={() => isClickable && onManagedNodeClick(node.deviceId!)}
                role={isClickable ? 'link' : undefined}
                tabIndex={isClickable ? 0 : undefined}
                onKeyDown={(event) => {
                  if (isClickable && (event.key === 'Enter' || event.key === ' ')) onManagedNodeClick(node.deviceId!)
                }}
              >
                <circle r="42" />
                <text className="topology-node-name" y="-2">{truncate(node.name, 19)}</text>
                <text className="topology-node-ip" y="15">{node.ipAddress ?? 'Discovered'}</text>
                {!node.isManaged && <text className="topology-node-tag" y="31">DISCOVERED</text>}
              </g>
            )
          })}
        </g>
      </svg>
    </div>
  )
}

function createLayout(nodes: TopologyNode[]) {
  const positions = new Map<string, Position>()
  if (nodes.length === 1) {
    positions.set(nodes[0].id, { x: GRAPH_WIDTH / 2, y: GRAPH_HEIGHT / 2 })
    return positions
  }

  nodes.forEach((node, index) => {
    const angle = (Math.PI * 2 * index) / nodes.length - Math.PI / 2
    positions.set(node.id, {
      x: GRAPH_WIDTH / 2 + Math.cos(angle) * 320,
      y: GRAPH_HEIGHT / 2 + Math.sin(angle) * 185,
    })
  })
  return positions
}

function truncate(value: string, maxLength: number) {
  return value.length > maxLength ? `${value.slice(0, maxLength - 1)}…` : value
}
