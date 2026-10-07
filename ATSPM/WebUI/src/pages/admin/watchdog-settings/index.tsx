import { ResponsivePageLayout } from '@/components/ResponsivePage'
import {
  PageNames,
  useUserHasClaim,
  useViewPage,
} from '@/features/identity/pagesCheck'
import { configAxios } from '@/lib/axios'
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  CircularProgress,
  FormControlLabel,
  MenuItem,
  Paper,
  Stack,
  Switch,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material'
import { useEffect, useState } from 'react'

type SettingValue = string | number | boolean
type Settings = Record<string, SettingValue>
type SettingsResponse = {
  saved: Settings
  effective: Settings
  overrides: string[]
}
type TimeZoneOption = { id: string; displayName: string }

// configAxios unwraps response.data in its response interceptor.
const getSettings = () => configAxios.get<SettingsResponse>('/WatchdogSettings') as unknown as Promise<SettingsResponse>
const getTimeZones = () => configAxios.get<TimeZoneOption[]>('/WatchdogSettings/time-zones') as unknown as Promise<TimeZoneOption[]>
const putSettings = (settings: Settings) => configAxios.put<SettingsResponse>('/WatchdogSettings', settings) as unknown as Promise<SettingsResponse>

const groups: { title: string; fields: { key: string; label: string; type: 'number' | 'boolean' | 'text' | 'time-zone' | 'select'; step?: string; options?: string[]; description?: string }[] }[] = [
  {
    title: 'Scan windows',
    fields: [
      { key: 'timeZoneId', label: 'Time zone', type: 'time-zone', description: 'Time zone used when interpreting scan hours and dates.' },
      { key: 'amStartHour', label: 'AM start hour', type: 'number', description: 'Hour when the morning scan window begins, using 24-hour time.' },
      { key: 'amEndHour', label: 'AM end hour', type: 'number', description: 'Hour when the morning scan window ends, using 24-hour time.' },
      { key: 'pmPeakStartHour', label: 'PM start hour', type: 'number', description: 'Hour when the afternoon/evening scan window begins, using 24-hour time.' },
      { key: 'pmPeakEndHour', label: 'PM end hour', type: 'number', description: 'Hour when the afternoon/evening scan window ends, using 24-hour time.' },
      { key: 'rampDetectorStartHour', label: 'Ramp detector start hour', type: 'number', description: 'Start hour for checking ramp detector errors.' },
      { key: 'rampDetectorEndHour', label: 'Ramp detector end hour', type: 'number', description: 'End hour for checking ramp detector errors.' },
      { key: 'rampMissedDetectorHitStartHour', label: 'Ramp missed-hit start hour', type: 'number', description: 'Start hour for checking missed ramp detector hits.' },
      { key: 'rampMissedDetectorHitEndHour', label: 'Ramp missed-hit end hour', type: 'number', description: 'End hour for checking missed ramp detector hits.' },
      { key: 'rampMainlineStartHour', label: 'Ramp mainline start hour', type: 'number', description: 'Start hour for checking ramp mainline conditions.' },
      { key: 'rampMainlineEndHour', label: 'Ramp mainline end hour', type: 'number', description: 'End hour for checking ramp mainline conditions.' },
      { key: 'rampStuckQueueStartHour', label: 'Ramp stuck-queue start hour', type: 'number', description: 'Start hour for checking stuck ramp queues.' },
      { key: 'rampStuckQueueEndHour', label: 'Ramp stuck-queue end hour', type: 'number', description: 'End hour for checking stuck ramp queues.' },
      { key: 'weekdayOnly', label: 'Weekdays only', type: 'boolean', description: 'Limit scheduled scans and related checks to weekdays.' },
    ],
  },
  {
    title: 'Detection thresholds',
    fields: [
      { key: 'consecutiveCount', label: 'Consecutive count', type: 'number', description: 'Number of consecutive occurrences required before an issue is reported.' },
      { key: 'minPhaseTerminations', label: 'Minimum phase terminations', type: 'number', description: 'Minimum phase terminations required for a phase-based check.' },
      { key: 'percentThreshold', label: 'Percent threshold (0–1)', type: 'number', step: '0.01', description: 'Fractional threshold used by percentage-based checks; enter a value from 0 to 1.' },
      { key: 'minimumRecords', label: 'Minimum records', type: 'number', description: 'Minimum number of records required before evaluating a check.' },
      { key: 'lowHitThreshold', label: 'Low-hit threshold', type: 'number', description: 'Number of hits at or below which a detector may be reported as low-hit.' },
      { key: 'lowHitRampThreshold', label: 'Ramp low-hit threshold', type: 'number', description: 'Low-hit threshold used specifically for ramp detectors.' },
      { key: 'maximumPedestrianEvents', label: 'Maximum pedestrian events', type: 'number', description: 'Maximum expected pedestrian events before a pedestrian issue is reported.' },
      { key: 'rampMissedEventsThreshold', label: 'Ramp missed-events threshold', type: 'number', description: 'Threshold for reporting missed ramp events.' },
    ],
  },
  {
    title: 'Email',
    fields: [
      { key: 'defaultEmailAddress', label: 'From address', type: 'text', description: 'The email address Watchdog uses as the sender for alert emails.' },
      { key: 'emailAllErrors', label: 'Email all errors', type: 'boolean', description: 'Send all supported error categories in the email report.' },
      { key: 'emailAmErrors', label: 'Email AM errors', type: 'boolean', description: 'Include errors detected during the AM scan window.' },
      { key: 'emailPmErrors', label: 'Email PM errors', type: 'boolean', description: 'Include errors detected during the PM scan window.' },
      { key: 'emailRampErrors', label: 'Email ramp errors', type: 'boolean', description: 'Include ramp and ramp-detector errors in the email report.' },
      { key: 'sort', label: 'Email sort field', type: 'select', options: ['Error', 'Consecutive', 'Location'], description: 'Controls issue order within each email section.' },
    ],
  },
]

export default function WatchdogSettingsPage() {
  const { isLoading: accessLoading } = useViewPage(PageNames.WatchdogSettings)
  const canEdit = useUserHasClaim('GeneralConfiguration:Edit')
  const [response, setResponse] = useState<SettingsResponse | null>(null)
  const [draft, setDraft] = useState<Settings>({})
  const [timeZones, setTimeZones] = useState<TimeZoneOption[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [savedMessage, setSavedMessage] = useState(false)

  useEffect(() => {
    if (accessLoading) return
    Promise.all([getSettings(), getTimeZones()])
      .then(([data, zones]) => {
        setResponse(data)
        setDraft(data.saved)
        setTimeZones(zones)
      })
      .catch(() => setError('Could not load WatchDog settings or time zones. Check ConfigApi and the database migration.'))
  }, [accessLoading])

  const save = async () => {
    setBusy(true)
    setError('')
    setSavedMessage(false)
    try {
      const data = await putSettings(draft)
      setResponse(data)
      setDraft(data.saved)
      setSavedMessage(true)
    } catch {
      setError('Could not save WatchDog settings. Check field values and your edit permission.')
    } finally {
      setBusy(false)
    }
  }

  if (accessLoading) return null

  return (
    <ResponsivePageLayout title="Watchdog Settings">
      <Stack spacing={2} sx={{ pb: 4 }}>
        <Typography color="text.secondary">
          Saved values live in the configuration database. Explicit WatchdogConfiguration keys in the WatchDog process take precedence for a scan. Overrides shown here are those visible to ConfigApi; WatchDog may have additional overrides in its own configuration.
        </Typography>
        {error && <Alert severity="error">{error}</Alert>}
        {savedMessage && <Alert severity="success">Settings saved. The next WatchDog scan will use them unless overridden by configuration.</Alert>}
        {!response ? <CircularProgress /> : <>
          {response.overrides.length > 0 && <Alert severity="warning">
            ConfigApi sees configuration overrides for: {response.overrides.join(', ')}. Check WatchDog configuration too; editing these saved values may not change the next scan until its overrides are removed.
          </Alert>}
          {groups.map((group) => <Paper key={group.title} variant="outlined" sx={{ p: 3, bgcolor: 'background.paper' }}>
            <Typography variant="h6" gutterBottom>{group.title}</Typography>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(2, minmax(0, 1fr))' }, gap: 2 }}>
              {group.fields.map((field) => {
                const overridden = response.overrides.some((key) => key.toLowerCase() === field.key.toLowerCase())
                const effective = response.effective[field.key]
                if (field.type === 'time-zone') {
                  const selectedId = String(draft[field.key] ?? '')
                  const options = selectedId && !timeZones.some((zone) => zone.id === selectedId)
                    ? [{ id: selectedId, displayName: 'Current saved value' }, ...timeZones]
                    : timeZones
                  return <Tooltip key={field.key} title={field.description ?? ''} placement="top" arrow enterDelay={1500} enterNextDelay={1500}>
                    <Autocomplete options={options} fullWidth size="small"
                    sx={{ gridColumn: '1 / -1' }}
                    value={options.find((zone) => zone.id === selectedId) ?? null}
                    getOptionLabel={(zone) => `${zone.id} — ${zone.displayName}`}
                    isOptionEqualToValue={(option, value) => option.id === value.id}
                    disabled={!canEdit || busy}
                    onChange={(_, zone) => setDraft({ ...draft, [field.key]: zone?.id ?? '' })}
                    renderInput={(params) => <TextField {...params} label={field.label}
                      helperText={overridden ? `ConfigApi override: ${String(effective)}` : 'Select a time zone recognized by ConfigApi.'} />}
                    />
                  </Tooltip>
                }
                if (field.type === 'boolean') return <Tooltip key={field.key} title={field.description ?? ''} placement="top" arrow enterDelay={1500} enterNextDelay={1500}>
                  <Box>
                  <FormControlLabel control={<Switch checked={Boolean(draft[field.key])} disabled={!canEdit || busy}
                    onChange={(event) => setDraft({ ...draft, [field.key]: event.target.checked })} />} label={field.label} />
                  {overridden && <Typography variant="caption" color="warning.main">ConfigApi override: {String(effective)}</Typography>}
                  </Box>
                </Tooltip>
                if (field.type === 'select') return <Tooltip key={field.key} title={field.description ?? ''} placement="top" arrow enterDelay={1500} enterNextDelay={1500}>
                  <TextField select fullWidth size="small" label={field.label}
                    value={draft[field.key] ?? field.options?.[0] ?? ''} disabled={!canEdit || busy}
                    helperText={overridden ? `ConfigApi override: ${String(effective)}` : undefined}
                    onChange={(event) => setDraft({ ...draft, [field.key]: event.target.value })}>
                    {field.options?.map((option) => <MenuItem key={option} value={option}>{option}</MenuItem>)}
                  </TextField>
                </Tooltip>
                return <Tooltip key={field.key} title={field.description ?? ''} placement="top" arrow enterDelay={1500} enterNextDelay={1500}>
                  <TextField fullWidth size="small" label={field.label}
                  type={field.type} value={draft[field.key] ?? ''} disabled={!canEdit || busy}
                  inputProps={field.type === 'number' ? { step: field.step ?? 1, min: 0 } : undefined}
                  helperText={overridden ? `ConfigApi override: ${String(effective)}` : undefined}
                  onChange={(event) => setDraft({ ...draft, [field.key]: field.type === 'number' ? Number(event.target.value) : event.target.value })} />
                </Tooltip>
              })}
            </Box>
          </Paper>)}
          <Typography variant="body2" color="text.secondary">Recipients are users assigned the WatchdogSubscriber role. SMTP host and credentials remain in application configuration.</Typography>
          {canEdit && <Box><Button variant="contained" disabled={busy} onClick={save}>Save settings</Button></Box>}
        </>}
      </Stack>
    </ResponsivePageLayout>
  )
}
