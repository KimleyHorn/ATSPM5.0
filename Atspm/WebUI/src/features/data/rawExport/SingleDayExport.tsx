import { Location } from '@/api/config'
import {
  getAggregationDataFromLocationIdentifierAndDataType,
  getEventLogDataFromLocationIdentifierAndDataType,
} from '@/api/data'
import SelectDateTime from '@/components/selectTimeSpan'
import { ResponseFormat } from '@/features/data/api/getEventLogs'
import { DataTypeOption } from '@/features/data/components/dataTypeSelector'
import { downloadData, generateFilename } from '@/features/data/utils'
import SelectLocation from '@/features/locations/components/selectLocation'
import useMissingDays from '@/hooks/useMissingDays'
import { useNotificationStore } from '@/stores/notifications'
import { dateToTimestamp } from '@/utils/dateTime'
import DownloadIcon from '@mui/icons-material/Download'
import { LoadingButton } from '@mui/lab'
import { Alert, Box, Paper, useMediaQuery, useTheme } from '@mui/material'
import {
  add,
  endOfMonth,
  endOfWeek,
  isValid,
  startOfMonth,
  startOfToday,
  startOfWeek,
} from 'date-fns'
import { ReactNode, useState } from 'react'
import { ExportFormat } from './types'

const buildRangeParams = (start: Date, end: Date) => ({
  start: isValid(start) ? dateToTimestamp(start) : '',
  end: isValid(end) ? dateToTimestamp(end) : '',
})

const mimeTypeForFormat = (format: ResponseFormat): string => {
  switch (format) {
    case 'json':
      return 'application/json'
    case 'xml':
      return 'application/xml'
    case 'csv':
    default:
      return 'text/csv'
  }
}

export interface SingleDayExportProps {
  selectedDataType: DataTypeOption
  downloadFormat: ExportFormat
  /** Data type dropdown, rendered by the page above the format options */
  dataTypeSelector: ReactNode
  /** CSV / JSON choice, rendered by the page */
  formatOptions: ReactNode
}

/**
 * The original export: one location and one day for any data type
 * (event logs of every kind, and aggregations).
 */
export default function SingleDayExport({
  selectedDataType,
  downloadFormat,
  dataTypeSelector,
  formatOptions,
}: SingleDayExportProps) {
  const theme = useTheme()
  const { addNotification } = useNotificationStore()
  const isMobileView = useMediaQuery(theme.breakpoints.down('md'))

  const [location, setLocation] = useState<Location | null>(null)
  const [error, setError] = useState(false)

  const [startDateTime, setStartDateTime] = useState(startOfToday())
  const [isDownloading, setIsDownloading] = useState(false)

  const [calendarStartDate, setCalendarStartDate] = useState<Date>(
    startOfWeek(startOfMonth(startOfToday()))
  )
  const [calendarEndDate, setCalendarEndDate] = useState<Date>(
    endOfWeek(endOfMonth(startOfToday()))
  )

  const missingDays = useMissingDays(
    location?.locationIdentifier ?? '',
    selectedDataType.name,
    selectedDataType.type,
    calendarStartDate,
    calendarEndDate
  )

  const handleStartDateTimeChange = (date: Date) => setStartDateTime(date)

  const handleCalendarRangeChange = (start: Date, end: Date) => {
    setCalendarStartDate(start)
    setCalendarEndDate(end)
  }

  const handleDateChange = (date: Date | null) => {
    if (!date) return
    const newStart = startOfWeek(startOfMonth(date))
    const newEnd = endOfWeek(endOfMonth(date))
    handleCalendarRangeChange(newStart, newEnd)
  }

  const downloadEventLogs = async () => {
    setIsDownloading(true)
    setError(false)

    const endDateTime = add(startDateTime, { days: 1 })

    try {
      const locationId = location?.locationIdentifier || ''
      const range = buildRangeParams(startDateTime, endDateTime)

      const fetchFn =
        selectedDataType.type === 'raw'
          ? getEventLogDataFromLocationIdentifierAndDataType
          : getAggregationDataFromLocationIdentifierAndDataType

      const data = await fetchFn(locationId, selectedDataType.name, range)

      const filename = generateFilename(
        location,
        selectedDataType,
        startDateTime,
        endDateTime,
        downloadFormat
      )

      const mimeType = mimeTypeForFormat(downloadFormat)
      await downloadData(data, filename, mimeType)
      addNotification({
        type: 'success',
        title: 'Download completed.',
      })
    } catch (err) {
      console.error('Error fetching data:', err)
      setError(true)
    } finally {
      setIsDownloading(false)
    }
  }

  const canDownload = !!location

  return (
    <>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
        <Paper
          sx={{
            flexGrow: 1,
            minWidth: '350px',
            p: 3,
          }}
        >
          <SelectLocation location={location} setLocation={setLocation} />
        </Paper>

        <Box display="flex" sx={{ gap: '15px', flexWrap: 'wrap' }}>
          <Paper
            sx={{
              p: 3,
              width: '336px',
              [theme.breakpoints.down('sm')]: {
                width: '100%',
              },
            }}
          >
            <SelectDateTime
              dateFormat="MMM d, yyyy"
              views={['year', 'month', 'day']}
              startDateTime={startDateTime}
              startDateOnly
              changeStartDate={handleStartDateTimeChange}
              noCalendar={isMobileView}
              markDays={missingDays}
              onChange={handleDateChange}
              onMonthChange={handleDateChange}
            />
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

      <Box sx={{ display: 'flex', gap: '10px', mt: '10px', mb: '120px' }}>
        <LoadingButton
          loadingPosition="start"
          startIcon={<DownloadIcon />}
          loading={isDownloading}
          variant="contained"
          onClick={downloadEventLogs}
          disabled={!canDownload}
        >
          Download Data
        </LoadingButton>

        {error && (
          <Box sx={{ ml: '1rem', mt: '19px', height: '48px' }}>
            <Alert severity="error">No Data Found</Alert>
          </Box>
        )}
      </Box>
    </>
  )
}
