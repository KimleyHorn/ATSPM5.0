import CloseIcon from '@mui/icons-material/Close'
import {
  Box,
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Tab,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tabs,
  Typography,
} from '@mui/material'
import type { EChartsOption } from 'echarts'
import { useEffect, useMemo, useState } from 'react'
import { buildDataViewTables } from './buildDataViewTables'
import RawDataTab from './RawDataTab'

// Large charts can have tens of thousands of points; rendering them all in a
// MUI table freezes the page, so each series shows the first rows only.
const MAX_ROWS_PER_SERIES = 500

// Chart titles are ECharts rich text such as "{title|Name}\n{dateTime|Range}".
// Strip the style wrappers and return the first two lines as plain text.
function getChartTitleLines(option: EChartsOption | null): {
  name: string
  subtitle: string
} {
  const title = Array.isArray(option?.title) ? option.title[0] : option?.title
  const text = typeof title?.text === 'string' ? title.text : ''
  // Strip the wrappers before splitting: a block can span several lines.
  const [name = '', subtitle = ''] = text
    .replace(/\{[^|}]*\|([^}]*)\}/g, '$1')
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
  return { name: name || 'Chart Data', subtitle }
}

export interface ChartDataDialogProps {
  open: boolean
  onClose: () => void
  option: EChartsOption | null
}

export default function ChartDataDialog({
  open,
  onClose,
  option,
}: ChartDataDialogProps) {
  const [tab, setTab] = useState<'data' | 'raw'>('raw')

  // Always open on the Raw Data tab
  useEffect(() => {
    if (open) setTab('raw')
  }, [open])

  const tables = useMemo(
    () => (open && option ? buildDataViewTables(option) : []),
    [open, option]
  )

  const heading = useMemo(() => getChartTitleLines(option), [option])

  return (
    <Dialog open={open} onClose={onClose} maxWidth="lg" fullWidth>
      <DialogTitle sx={{ display: 'flex', alignItems: 'center', pr: 1 }}>
        <Box sx={{ flexGrow: 1 }}>
          <Typography variant="h6" component="span" display="block">
            {heading.name}
          </Typography>
          {heading.subtitle && (
            <Typography
              variant="body2"
              color="text.secondary"
              component="span"
              display="block"
            >
              {heading.subtitle}
            </Typography>
          )}
        </Box>
        <IconButton aria-label="Close" onClick={onClose}>
          <CloseIcon />
        </IconButton>
      </DialogTitle>
      <Tabs
        value={tab}
        onChange={(_, value) => setTab(value)}
        sx={{ px: 3, borderBottom: 1, borderColor: 'divider' }}
      >
        <Tab label="Raw Data" value="raw" />
        <Tab label="Data View" value="data" />
      </Tabs>
      <DialogContent
        sx={{ minHeight: 300, colorScheme: (theme) => theme.palette.mode }}
      >
        {tab === 'data' &&
          (tables.length === 0 ? (
            <Typography color="text.secondary">
              No data available for this chart.
            </Typography>
          ) : (
            tables.map((table, index) => (
              <Box key={`${table.name}-${index}`} sx={{ mb: 3 }}>
                <Typography variant="subtitle1" fontWeight="bold" gutterBottom>
                  {table.name}
                </Typography>
                <TableContainer
                  sx={{
                    maxHeight: 360,
                    colorScheme: (theme) => theme.palette.mode,
                  }}
                >
                  <Table size="small" stickyHeader>
                    <TableHead>
                      <TableRow>
                        {table.columns.map((column) => (
                          <TableCell key={column}>{column}</TableCell>
                        ))}
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {table.rows.slice(0, MAX_ROWS_PER_SERIES).map((row, i) => (
                        <TableRow key={i}>
                          {table.columns.map((column, c) => (
                            <TableCell key={column}>{row[c] ?? ''}</TableCell>
                          ))}
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </TableContainer>
                {table.rows.length > MAX_ROWS_PER_SERIES && (
                  <Typography variant="caption" color="text.secondary">
                    Showing the first {MAX_ROWS_PER_SERIES} of{' '}
                    {table.rows.length} rows.
                  </Typography>
                )}
              </Box>
            ))
          ))}
        {tab === 'raw' && <RawDataTab title={heading.name} />}
      </DialogContent>
    </Dialog>
  )
}
