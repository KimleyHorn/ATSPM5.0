import { generatePin } from '@/features/locations/utils'
import { useEnv } from '@/hooks/useEnv'
import type L from 'leaflet'
import { useEffect, useState } from 'react'
import { MapContainer, Marker, TileLayer, Tooltip, useMapEvents } from 'react-leaflet'

type Coordinates = [number, number]

function MapClickHandler({ onSelect }: { onSelect: (point: Coordinates) => void }) {
  useMapEvents({
    click(event) {
      onSelect([event.latlng.lat, event.latlng.lng])
    },
  })
  return null
}

export default function CopyLocationMap({
  center,
  source,
  sourceLabel,
  selected,
  onSelect,
}: {
  center: Coordinates
  source: Coordinates | null
  sourceLabel: string
  selected: Coordinates | null
  onSelect: (point: Coordinates) => void
}) {
  const { data: env } = useEnv()
  const [sourceIcon, setSourceIcon] = useState<L.DivIcon | null>(null)
  const [newLocationIcon, setNewLocationIcon] = useState<L.DivIcon | null>(null)

  useEffect(() => {
    let active = true
    Promise.all([generatePin(1), import('leaflet')]).then(([icon, Leaflet]) => {
      if (!active) return
      setSourceIcon(icon)
      setNewLocationIcon(
        Leaflet.divIcon({
          className: 'copy-location-new-marker',
          html: '<svg width="28" height="42" viewBox="0 0 28 42" xmlns="http://www.w3.org/2000/svg"><path d="M14 1C6.8 1 1 6.8 1 14c0 10 13 27 13 27s13-17 13-27C27 6.8 21.2 1 14 1Z" fill="#1565c0" stroke="white" stroke-width="2"/><circle cx="14" cy="14" r="5" fill="white"/></svg>',
          iconSize: [28, 42],
          iconAnchor: [14, 42],
        })
      )
    })
    return () => {
      active = false
    }
  }, [])

  if (!env?.MAP_TILE_LAYER || !env.MAP_TILE_ATTRIBUTION) return null

  return (
    <MapContainer
      center={center}
      zoom={17}
      style={{ height: '100%', width: '100%' }}
      scrollWheelZoom
    >
      <TileLayer
        attribution={env.MAP_TILE_ATTRIBUTION}
        url={env.MAP_TILE_LAYER}
      />
      {source && sourceIcon && (
        <Marker position={source} icon={sourceIcon}>
          <Tooltip permanent direction="top">
            Original: {sourceLabel}
          </Tooltip>
        </Marker>
      )}
      {selected && newLocationIcon && (
        <Marker
          position={selected}
          icon={newLocationIcon}
          draggable
          eventHandlers={{
            dragend(event) {
              const point = event.target.getLatLng()
              onSelect([point.lat, point.lng])
            },
          }}
        >
          <Tooltip permanent direction="top">
            New location
          </Tooltip>
        </Marker>
      )}
      <MapClickHandler onSelect={onSelect} />
    </MapContainer>
  )
}
