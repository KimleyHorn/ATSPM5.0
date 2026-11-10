import { reportsAxios } from '@/lib/axios'
import { dateToTimestamp } from '@/utils/dateTime'

export interface RawEventsRequest {
  /** A single location. Use `locationIdentifiers` for several. */
  locationIdentifier?: string
  locationIdentifiers?: string[]
  start: Date
  end: Date
  /** Time of day to keep on every day in the range, as HH:mm */
  dailyStartTime?: string
  /** End of the time-of-day window (exclusive), as HH:mm */
  dailyEndTime?: string
  eventCodes?: number[]
  eventParams?: number[]
  maxRows?: number
}

export interface RawEvent {
  locationIdentifier: string
  eventCode: number
  eventCodeName: string
  eventParam: number
  timestamp: string
}

export interface RawEventsResult {
  events: RawEvent[]
  totalCount: number
  truncated: boolean
}

export interface EventCodeInfo {
  code: number
  name: string
  category: string
  paramType: string
}

/**
 * Gets the underlying event log records for one or more locations and a time
 * range. Used by the chart Raw Data view and the Export Data page.
 */
export const getRawEvents = async (
  request: RawEventsRequest
): Promise<RawEventsResult> => {
  // The axios instance unwraps the response, so this is the result body.
  return (await reportsAxios.post('/api/v1/RawEvents/getRawData', {
    ...request,
    start: dateToTimestamp(request.start),
    end: dateToTimestamp(request.end),
  })) as unknown as RawEventsResult
}

/** Every known event code with its readable name and category. */
export const getEventCodes = async (): Promise<EventCodeInfo[]> => {
  return (await reportsAxios.get(
    '/api/v1/RawEvents/getEventCodes'
  )) as unknown as EventCodeInfo[]
}
