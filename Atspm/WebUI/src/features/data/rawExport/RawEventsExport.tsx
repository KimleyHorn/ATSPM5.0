import {
  EventCodeInfo,
  RawEventsRequest,
  getRawEvents,
} from '@/features/charts/api/getRawEvents'
import {
  buildRawEventsCsv,
  buildRawEventsJson,
  downloadTextFile,
} from '@/features/charts/components/chartDataDialog/rawEventsCsv'
import { Location } from '@/features/locations/types'
import { useNotificationStore } from '@/stores/notifications'
import DownloadIcon from '@mui/icons-material/Download'
import { LoadingButton } from '@mui/lab'
import {
  Alert,
  Box,
  Checkbox,
  FormControlLabel,
  Paper,
  Typography,
  useTheme,
} from '@mui/material'
import { DatePicker } from '@mui/x-date-pickers/DatePicker'
import { TimePicker } from '@mui/x-date-pickers/TimePicker'
import {
  addDays,
  format,
  isValid,
  set,
  startOfDay,
  startOfToday,
  subDays,
} from 'date-fns'
import { ReactNode, useState } from 'react'
import EventCodeSelect from './EventCodeSelect'
import MultiLocationPicker from './MultiLocationPicker'
import { ExportFormat } from './types'

// The most rows the API will return for one export
const MAX_EXPORT_ROWS = 250_000

const MIME_TYPES: Record<ExportFormat, string> = {
  csv: 'text/csv;charset=utf-8',
  json: 'application/json',
}

const defaultTime = (hours: number) =>
  set(startOfToday(), { hours, minutes: 0, seconds: 0, milliseconds: 0 })

export interface RawEventsExportProps {
  downloadFormat: ExportFormat
  /** Data type dropdown, rendered by the page at the top of the right column */
  dataTypeSelector: ReactNode
  /** CSV / JSON choice, rendered by the page under the dates */
  formatOptions: ReactNode
}

/**
 * Export of raw controller (Indiana) events for several locations, a date range,
 * an optional time of day and optional event codes.
 */
export default function RawEventsExport({
  downloadFormat,
  dataTypeSelector,
  formatOptions,
}: RawEventsExportProps) {
  const theme = useTheme()
  const { addNotification } = useNotificationStore()

  const [locations, setLocations] = useState<Location[]>([])
  const [eventCodes, setEventCodes] = useState<EventCodeInfo[]>([])

  const yesterday = subDays(startOfToday(), 1)
  const [startDate, setStartDate] = useState<Date | null>(yesterday)
  const [endDate, setEndDate] = useState<Date | null>(yesterday)

  const [limitTime, setLimitTime] = useState(false)
  const [startTime, setStartTime] = useState<Date | null>(defaultTime(6))
  const [endTime, setEndTime] = useState<Date | null>(defaultTime(9))

  const [isDownloading, setIsDownloading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const datesValid =
    !!startDate &&
    !!endDate &&
    isValid(startDate) &&
    isValid(endDate) &&
    startOfDay(endDate) >= startOfDay(startDate)

  const timesValid =
    !limitTime ||
    (!!startTime &&
      !!endTime &&
      isValid(startTime) &&
      isValid(endTime) &&
      format(startTime, 'HH:mm') !== format(endTime, 'HH:mm'))

  const canDownload = locations.length > 0 && datesValid && timesValid

  const downloadEvents = async () => {
    if (!canDownload || !startDate || !endDate) return

    setIsDownloading(true)
    setError(null)
    setNotice(null)

    const request: RawEventsRequest = {
      locationIdentifiers: locations
        .map((l) => l.locationIdentifier)
        .filter((id): id is string => !!id),
      start: startOfDay(startDate),
      // Whole days: the end date is included, so stop at the next midnight
      end: addDays(startOfDay(endDate), 1),
      eventCodes: eventCodes.length ? eventCodes.map((c) => c.code) : undefined,
      maxRows: MAX_EXPORT_ROWS,
    }
    if (limitTime && startTime && endTime) {
      request.dailyStartTime = format(startTime, 'HH:mm')
      request.dailyEndTime = format(endTime, 'HH:mm')
    }

    try {
      const result = await getRawEvents(request)

      if (result.events.length === 0) {
        setError('No events were found for these selections.')
        return
      }

      const text =
        downloadFormat === 'csv'
          ? buildRawEventsCsv(result.events)
          : buildRawEventsJson(result.events)

      const place =
        locations.length === 1
          ? locations[0].locationIdentifier
          : `${locations.length}_locations`
      const filename = `RawEvents_${place}_${format(startDate, 'yyyy-MM-dd')}_to_${format(endDate, 'yyyy-MM-dd')}.${downloadFormat}`

      downloadTextFile(filename, text, MIME_TYPES[downloadFormat])

      if (result.truncated) {
        setNotice(
          `Downloaded the first ${result.events.length.toLocaleString()} of ${result.totalCount.toLocaleString()} matching events. Narrow the locations, dates, times or event codes to get the rest.`
        )
      } else {
        addNotification({ type: 'success', title: 'Download completed.' })
      }
    } catch (err) {
      console.error('Error exporting events:', err)
      setError(
        typeof err === 'string'
          ? err
          : err instanceof Error
            ? err.message
            : 'Unable to export events.'
      )
    } finally {
      setIsDownloading(false)
    }
  }

  return (
    <>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
        <Box
          sx={{
            display: 'flex',
            flexDirection: 'column',
            gap: 2,
            // A fixed starting width, so a long message (like the row-limit
            // warning) can't make this column grow and push the others below
            flex: '1 1 350px',
            minWidth: 0,
            [theme.breakpoints.down('sm')]: { flexBasis: '100%' },
          }}
        >
          <Paper sx={{ p: 3 }}>
            <MultiLocationPicker value={locations} onChange={setLocations} />
          </Paper>
          <Paper sx={{ p: 3 }}>
            <EventCodeSelect value={eventCodes} onChange={setEventCodes} />
          </Paper>

          <Box
            sx={{
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
              flexWrap: 'wrap',
            }}
          >
            <LoadingButton
              loadingPosition="start"
              startIcon={<DownloadIcon />}
              loading={isDownloading}
              variant="contained"
              onClick={downloadEvents}
              disabled={!canDownload}
            >
              Download Data
            </LoadingButton>

            {error && <Alert severity="error">{error}</Alert>}
            {notice && <Alert severity="warning">{notice}</Alert>}
          </Box>
        </Box>

        {/* Same arrangement as the single-day export so the dates stay in the
            same spot when the data type changes */}
        <Box display="flex" sx={{ gap: '15px', flexWrap: 'wrap' }}>
          <Paper
            sx={{
              p: 3,
              width: '336px',
              // Only as tall as its contents, not stretched to the tallest column
              alignSelf: 'flex-start',
              display: 'flex',
              flexDirection: 'column',
              gap: 2,
              [theme.breakpoints.down('sm')]: { width: '100%' },
            }}
          >
            <Typography variant="subtitle1" fontWeight="bold">
              Dates
            </Typography>
            <DatePicker
              label="Start date"
              value={startDate}
              onChange={(value) => setStartDate(value)}
              maxDate={endDate ?? undefined}
            />
            <DatePicker
              label="End date"
              value={endDate}
              onChange={(value) => setEndDate(value)}
              minDate={startDate ?? undefined}
            />

            <FormControlLabel
              control={
                <Checkbox
                  checked={limitTime}
                  onChange={(e) => setLimitTime(e.target.checked)}
                />
              }
              label="Only include a time of day"
            />
            {limitTime && (
              <>
                <TimePicker
                  label="From"
                  ampm={false}
                  value={startTime}
                  onChange={(value) => setStartTime(value)}
                />
                <TimePicker
                  label="To"
                  ampm={false}
                  value={endTime}
                  onChange={(value) => setEndTime(value)}
                />
                <Typography variant="caption" color="text.secondary">
                  Applies to every day in the range. A start later than the end
                  runs past midnight.
                </Typography>
                {!timesValid && (
                  <Alert severity="warning">Choose two different times.</Alert>
                )}
              </>
            )}
          </Paper>

          <Box
            sx={{
              minWidth: '23.188rem',
              [theme.breakpoints.down('sm')]: { width: '100%' },
              display: 'flex',
              flexDirection: 'column',
              gap: 2,
            }}
          >
            {dataTypeSelector}
            {formatOptions}
          </Box>
        </Box>
      </Box>
      <Box sx={{ mb: '120px' }} />
    </>
  )
}
