import type { RawEvent } from '@/features/charts/api/getRawEvents'

const csvEscape = (value: unknown): string => {
  const text = String(value ?? '')
  return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
}

// "2026-09-01T12:00:17.3" -> "2026-09-01 12:00:17.3"
// Note: Excel may still display values with fractions of a second as mm:ss.0
// when a CSV is opened by double-click. The full date and time is in the cell.
export const formatEventTimestamp = (timestamp: string): string =>
  timestamp.replace('T', ' ')

export function buildRawEventsCsv(events: RawEvent[]): string {
  const header = [
    'Signal ID',
    'Event Code',
    'Event Code Name',
    'Event Param',
    'Timestamp',
  ]
  const rows = events.map((e) => [
    e.locationIdentifier,
    e.eventCode,
    e.eventCodeName,
    e.eventParam,
    formatEventTimestamp(e.timestamp),
  ])
  return [header, ...rows].map((row) => row.map(csvEscape).join(',')).join('\r\n')
}

export function buildRawEventsJson(events: RawEvent[]): string {
  return JSON.stringify(
    events.map((e) => ({
      signalId: e.locationIdentifier,
      eventCode: e.eventCode,
      eventCodeName: e.eventCodeName,
      eventParam: e.eventParam,
      timestamp: formatEventTimestamp(e.timestamp),
    })),
    null,
    2
  )
}

export function downloadTextFile(
  filename: string,
  text: string,
  mime = 'text/csv;charset=utf-8'
) {
  // Leading BOM so Excel opens CSV files as UTF-8. JSON must not have one.
  const parts = mime.startsWith('text/csv') ? ['﻿', text] : [text]
  const blob = new Blob(parts, { type: mime })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}
