import type { EChartsOption } from 'echarts'

export interface DataViewTable {
  name: string
  columns: string[]
  rows: string[][]
}

const formatCell = (value: unknown): string => {
  if (value === null || value === undefined) return ''
  if (value instanceof Date) return value.toISOString()
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

const toArray = <T>(value: T | T[] | undefined): T[] =>
  value === undefined ? [] : Array.isArray(value) ? value : [value]

/**
 * Builds one table per series from a chart option returned by
 * `chartInstance.getOption()`. This is the aggregate "Data View": it reflects
 * exactly what is plotted, the same as ECharts' built-in data view did.
 */
export function buildDataViewTables(option: EChartsOption): DataViewTable[] {
  const xAxisData = toArray(option.xAxis as { data?: unknown[] } | undefined)[0]
    ?.data

  const tables: DataViewTable[] = []

  toArray(option.series as { name?: string; data?: unknown[] }[]).forEach(
    (series, seriesIndex) => {
      if (!Array.isArray(series?.data) || series.data.length === 0) return

      const rows: string[][] = []
      let width = 0

      series.data.forEach((point, pointIndex) => {
        if (point === null || point === undefined) return
        const raw =
          typeof point === 'object' && !Array.isArray(point) && 'value' in point
            ? (point as { value: unknown }).value
            : point

        let cells: unknown[]
        if (Array.isArray(raw)) {
          cells = raw
        } else if (xAxisData && xAxisData[pointIndex] !== undefined) {
          cells = [xAxisData[pointIndex], raw]
        } else {
          cells = [raw]
        }

        width = Math.max(width, cells.length)
        rows.push(cells.map(formatCell))
      })

      if (rows.length === 0) return

      const columns =
        width === 1
          ? ['Value']
          : width === 2
            ? ['X', 'Y']
            : Array.from({ length: width }, (_, i) => `Value ${i + 1}`)

      tables.push({
        name: series.name || `Series ${seriesIndex + 1}`,
        columns,
        rows,
      })
    }
  )

  return tables
}
