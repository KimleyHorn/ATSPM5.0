import { Device, Location } from '@/api/config'
import { getLocation } from '@/pages/admin/locations/[[...id]]'
import { configRequest } from '@/lib/axios'
import { useEnv } from '@/hooks/useEnv'
import { useNotificationStore } from '@/stores/notifications'
import PinDropIcon from '@mui/icons-material/PinDrop'
import { LoadingButton } from '@mui/lab'
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Skeleton,
  TextField,
  Typography,
} from '@mui/material'
import { isAxiosError } from 'axios'
import dynamic from 'next/dynamic'
import { FormEvent, useEffect, useState } from 'react'
import { useQueryClient } from 'react-query'

interface CopyDevice {
  sourceDeviceId: number
  ipaddress: string
  label: string
  currentIpaddress: string
}

type Coordinates = [number, number]

const CopyLocationMap = dynamic(() => import('./CopyLocationMap'), {
  ssr: false,
  loading: () => <Skeleton variant="rectangular" height={600} />,
})

interface SourceOption {
  id: number
  locationIdentifier: string
  primaryName?: string
  secondaryName?: string
}

const ipAddressIsValid = (value: string) => {
  const parts = value.trim().split('.')
  return (
    parts.length === 4 &&
    parts.every((part) => /^\d{1,3}$/.test(part) && Number(part) <= 255)
  )
}

export default function CopyLocationDialog({
  locations,
  onCreated,
  onClose,
}: {
  locations: SourceOption[]
  onCreated: (id: number) => void
  onClose: () => void
}) {
  const addNotification = useNotificationStore((state) => state.addNotification)
  const queryClient = useQueryClient()
  const { data: env } = useEnv()
  const [sourceId, setSourceId] = useState<number | null>(null)
  const [source, setSource] = useState<Location | null>(null)
  const [identifier, setIdentifier] = useState('')
  const [primaryName, setPrimaryName] = useState('')
  const [secondaryName, setSecondaryName] = useState('')
  const [latitude, setLatitude] = useState('')
  const [longitude, setLongitude] = useState('')
  const [devices, setDevices] = useState<CopyDevice[]>([])
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [mapOpen, setMapOpen] = useState(false)
  const [pendingCoordinates, setPendingCoordinates] =
    useState<Coordinates | null>(null)

  useEffect(() => {
    if (!sourceId) {
      setSource(null)
      setDevices([])
      return
    }
    let active = true
    setLoading(true)
    setError('')
    setSource(null)
    setDevices([])
    getLocation(sourceId)
      .then((source) => {
        if (!active) return
        if (!source) {
          setError('Could not load the selected intersection.')
          return
        }
        setSource(source)
        setDevices(
          (source?.devices || []).map((device: Device) => ({
            sourceDeviceId: device.id ?? 0,
            ipaddress: '',
            label: device.deviceIdentifier || `Device ${device.id}`,
            currentIpaddress: device.ipaddress || '',
          }))
        )
      })
      .catch(() => active && setError('Could not load the source devices.'))
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [sourceId])

  const coordinatesValid =
    latitude.trim() !== '' &&
    longitude.trim() !== '' &&
    Number(latitude) >= -90 &&
    Number(latitude) <= 90 &&
    Number(longitude) >= -180 &&
    Number(longitude) <= 180
  const sourceCoordinates: Coordinates | null =
    source &&
    Number.isFinite(source.latitude) &&
    Number.isFinite(source.longitude) &&
    (source.latitude !== 0 || source.longitude !== 0)
      ? [source.latitude!, source.longitude!]
      : null
  const defaultLatitude = Number(env?.MAP_DEFAULT_LATITUDE)
  const defaultLongitude = Number(env?.MAP_DEFAULT_LONGITUDE)
  const mapCenter: Coordinates = sourceCoordinates ||
    (Number.isFinite(defaultLatitude) && Number.isFinite(defaultLongitude)
      ? [defaultLatitude, defaultLongitude]
      : [0, 0])
  const devicesValid = devices.every((device) =>
    ipAddressIsValid(device.ipaddress)
  )
  const canCopy =
    !!source &&
    identifier.trim().length > 0 &&
    identifier.trim().length <= 10 &&
    identifier.trim() !== source.locationIdentifier &&
    primaryName.trim().length > 0 &&
    primaryName.length <= 100 &&
    secondaryName.length <= 100 &&
    coordinatesValid &&
    devicesValid &&
    !loading &&
    !saving

  const updateDevice = (index: number, patch: Partial<CopyDevice>) => {
    setDevices((current) =>
      current.map((device, i) =>
        i === index ? { ...device, ...patch } : device
      )
    )
  }

  const openMap = () => {
    setPendingCoordinates(
      coordinatesValid ? [Number(latitude), Number(longitude)] : null
    )
    setMapOpen(true)
  }

  const confirmMapSelection = () => {
    if (!pendingCoordinates) return
    setLatitude(pendingCoordinates[0].toFixed(6))
    setLongitude(pendingCoordinates[1].toFixed(6))
    setMapOpen(false)
  }

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    if (!canCopy) return
    setSaving(true)
    setError('')
    try {
      const copied = await configRequest<{ id: number }>({
        url: `/Location/${sourceId}/CopyLocation`,
        method: 'POST',
        data: {
          locationIdentifier: identifier.trim(),
          primaryName,
          secondaryName,
          latitude: Number(latitude),
          longitude: Number(longitude),
          devices: devices.map(({ sourceDeviceId, ipaddress }) => ({
            sourceDeviceId,
            ipaddress: ipaddress.trim(),
          })),
        },
      })
      if (!copied.id)
        throw new Error('The copy response did not include an ID.')
      await queryClient.invalidateQueries(['locations'])
      addNotification({ type: 'success', title: 'Location Copied' })
      onCreated(copied.id)
    } catch (cause) {
      const response = isAxiosError(cause) ? cause.response?.data : null
      setError(
        typeof response === 'string'
          ? response
          : 'Could not copy the location. Check the new ID and device addresses.'
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <form onSubmit={submit}>
        <DialogTitle>Copy Location</DialogTitle>
        <DialogContent>
          <Typography sx={{ mb: 2 }}>
            Choose an intersection to copy, then enter the new signal details.
          </Typography>
          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}
          <Box sx={{ display: 'grid', gap: 2, pt: 1 }}>
            <Autocomplete
              options={locations}
              value={locations.find((option) => option.id === sourceId) || null}
              onChange={(_, option) => {
                setSourceId(option?.id ?? null)
                setPrimaryName('')
                setSecondaryName('')
                setLatitude('')
                setLongitude('')
              }}
              getOptionLabel={(option) =>
                `${option.locationIdentifier} - ${option.primaryName || ''} ${option.secondaryName || ''}`
              }
              isOptionEqualToValue={(option, value) => option.id === value.id}
              renderInput={(params) => (
                <TextField
                  {...params}
                  label="Copy existing intersection"
                  required
                />
              )}
            />
            {loading && <Typography>Loading intersection configuration...</Typography>}
            {source && (
              <Typography variant="body2">New signal details</Typography>
            )}
            <TextField
              label="New Signal ID"
              required
              autoFocus
              value={identifier}
              onChange={(event) => setIdentifier(event.target.value)}
              inputProps={{ maxLength: 10 }}
              error={
                !!source && identifier.trim() === source.locationIdentifier
              }
              helperText={
                source && identifier.trim() === source.locationIdentifier
                  ? 'Enter an ID different from the source signal.'
                  : 'Must be unique and 10 characters or fewer.'
              }
            />
            <TextField
              label="Primary Street / Name"
              required
              value={primaryName}
              onChange={(event) => setPrimaryName(event.target.value)}
            />
            <TextField
              label="Secondary Street / Name"
              value={secondaryName}
              onChange={(event) => setSecondaryName(event.target.value)}
            />
            <TextField
              label="Latitude"
              type="number"
              required
              value={latitude}
              onChange={(event) => setLatitude(event.target.value)}
              inputProps={{ step: 'any', min: -90, max: 90 }}
            />
            <TextField
              label="Longitude"
              type="number"
              required
              value={longitude}
              onChange={(event) => setLongitude(event.target.value)}
              inputProps={{ step: 'any', min: -180, max: 180 }}
            />
            <Button
              variant="outlined"
              startIcon={<PinDropIcon />}
              onClick={openMap}
              disabled={!source}
            >
              Select New Location on Map
            </Button>
            {sourceCoordinates && (
              <Typography variant="caption" color="text.secondary">
                Original signal: {sourceCoordinates[0].toFixed(6)},{' '}
                {sourceCoordinates[1].toFixed(6)}
              </Typography>
            )}
            {devices.map((device, index) => (
              <Box
                key={device.sourceDeviceId}
                sx={{
                  display: 'grid',
                  gap: 2,
                  borderTop: 1,
                  borderColor: 'divider',
                  pt: 2,
                }}
              >
                <Typography variant="subtitle1">
                  {device.label} (current IP: {device.currentIpaddress || 'none'})
                </Typography>
                <TextField
                  label="New IP Address"
                  required
                  value={device.ipaddress}
                  onChange={(event) =>
                    updateDevice(index, { ipaddress: event.target.value })
                  }
                  error={
                    !!device.ipaddress && !ipAddressIsValid(device.ipaddress)
                  }
                  helperText="Enter the address for the copied device."
                />
              </Box>
            ))}
          </Box>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} disabled={saving}>
            Cancel
          </Button>
          <LoadingButton
            type="submit"
            variant="contained"
            loading={saving}
            disabled={!canCopy}
          >
            Copy Location
          </LoadingButton>
        </DialogActions>
      </form>
      <Dialog
        open={mapOpen}
        onClose={() => setMapOpen(false)}
        maxWidth="lg"
        fullWidth
      >
        <DialogTitle>Select New Location on Map</DialogTitle>
        <DialogContent sx={{ height: 650, p: 2 }}>
          {mapOpen && (
            <CopyLocationMap
              key={sourceId}
              center={mapCenter}
              source={sourceCoordinates}
              sourceLabel={source?.locationIdentifier || ''}
              selected={pendingCoordinates}
              onSelect={setPendingCoordinates}
            />
          )}
        </DialogContent>
        <DialogActions>
          <Typography sx={{ flexGrow: 1, ml: 2 }}>
            {pendingCoordinates
              ? `New location: ${pendingCoordinates[0].toFixed(6)}, ${pendingCoordinates[1].toFixed(6)}`
              : 'Click the map to place the new location.'}
          </Typography>
          <Button onClick={() => setMapOpen(false)}>Cancel</Button>
          <Button
            onClick={confirmMapSelection}
            variant="contained"
            disabled={!pendingCoordinates}
          >
            Use Coordinates
          </Button>
        </DialogActions>
      </Dialog>
    </Dialog>
  )
}
