import { getRawEvents } from '@/features/charts/api/getRawEvents'
import { formatExportFileName } from '@/features/charts/common/transformers'
import { useChartsStore } from '@/stores/charts'
import DownloadIcon from '@mui/icons-material/Download'
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from '@mui/material'
import { useQuery } from 'react-query'
import {
  buildRawEventsCsv,
  downloadTextFile,
  formatEventTimestamp,
} from './rawEventsCsv'

// Rendering every row in a MUI table freezes the page. The download always
// contains every row the API returned.
const MAX_DISPLAY_ROWS = 1000

export interface RawDataTabProps {
  /** Chart name, used to name the downloaded file */
  title: string
}

export default function RawDataTab({ title }: RawDataTabProps) {
  const request = useChartsStore((state) => state.chartRequest)

  const { data, isLoading, isError, error } = useQuery(
    [
      'rawEvents',
      request?.locationIdentifier,
      request?.start.getTime(),
      request?.end.getTime(),
    ],
    () => getRawEvents(request!),
    { enabled: !!request, staleTime: 60_000, refetchOnWindowFocus: false }
  )

  if (!request) {
    return (
      <Typography color="text.secondary">
        Raw data is not available for this chart.
      </Typography>
    )
  }

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
        <CircularProgress />
      </Box>
    )
  }

  if (isError || !data) {
    return (
      <Alert severity="error">
        {error instanceof Error
          ? error.message
          : 'Unable to load the raw event data.'}
      </Alert>
    )
  }

  if (data.events.length === 0) {
    return (
      <Typography color="text.secondary">
        No events were found for this location and time range.
      </Typography>
    )
  }

  const handleDownload = () => {
    const filename = `${formatExportFileName(
      `${title} Raw Data`,
      request.start.toISOString(),
      request.end.toISOString()
    )}.csv`
    downloadTextFile(filename, buildRawEventsCsv(data.events))
  }

  return (
    <Box>
      <Box
        sx={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          mb: 1,
          gap: 2,
        }}
      >
        <Typography variant="body2" color="text.secondary">
          {data.truncated
            ? `Showing the first ${data.events.length.toLocaleString()} of ${data.totalCount.toLocaleString()} events. Choose a shorter time range to see the rest.`
            : `${data.totalCount.toLocaleString()} events`}
        </Typography>
        <Button
          variant="outlined"
          size="small"
          startIcon={<DownloadIcon />}
          onClick={handleDownload}
        >
          Download CSV
        </Button>
      </Box>
      <TableContainer
        sx={{ maxHeight: 420, colorScheme: (theme) => theme.palette.mode }}
      >
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow>
              <TableCell>Signal ID</TableCell>
              <TableCell>Event Code</TableCell>
              <TableCell>Event Code Name</TableCell>
              <TableCell>Event Param</TableCell>
              <TableCell>Timestamp</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data.events.slice(0, MAX_DISPLAY_ROWS).map((event, index) => (
              <TableRow key={index}>
                <TableCell>{event.locationIdentifier}</TableCell>
                <TableCell>{event.eventCode}</TableCell>
                <TableCell>{event.eventCodeName}</TableCell>
                <TableCell>{event.eventParam}</TableCell>
                <TableCell>{formatEventTimestamp(event.timestamp)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {data.events.length > MAX_DISPLAY_ROWS && (
        <Typography variant="caption" color="text.secondary">
          The table shows the first {MAX_DISPLAY_ROWS.toLocaleString()} rows.
          The download includes all {data.events.length.toLocaleString()}.
        </Typography>
      )}
    </Box>
  )
}
