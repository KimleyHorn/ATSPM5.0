#region license
// Copyright 2026 Utah Departement of Transportation
// for ReportApi - Utah.Udot.Atspm.ReportApi.Controllers/RawEventsController.cs
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Utah.Udot.Atspm.Data.Models.EventLogModels;
using Utah.Udot.Atspm.ReportApi.EventCodes;
using Utah.Udot.Atspm.Repositories.EventLogRepositories;

namespace Utah.Udot.Atspm.ReportApi.Controllers
{
    /// <summary>
    /// Returns raw event log records. Used by the chart "Raw Data" view and by the
    /// Export Data page.
    /// </summary>
    [ApiController]
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class RawEventsController : ControllerBase
    {
        /// <summary>Rows returned when the request does not set <see cref="RawEventsOptions.MaxRows"/>.</summary>
        public const int DefaultMaxRows = 50_000;

        /// <summary>Upper limit on <see cref="RawEventsOptions.MaxRows"/>.</summary>
        public const int MaxAllowedRows = 250_000;

        /// <summary>Most locations a single request may ask for.</summary>
        public const int MaxLocations = 100;

        private static readonly string[] TimeFormats = { "HH:mm", "HH:mm:ss" };

        private readonly IIndianaEventLogRepository _eventLogRepository;
        private readonly ILogger<RawEventsController> _logger;

        /// <inheritdoc/>
        public RawEventsController(IIndianaEventLogRepository eventLogRepository, ILogger<RawEventsController> logger)
        {
            _eventLogRepository = eventLogRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get raw event log records for one or more locations and a time range, optionally
        /// limited to specific event codes, event parameters and a time of day.
        /// </summary>
        /// <param name="options">Locations, time range and optional filters</param>
        /// <returns>Records ordered by location then timestamp, with event code names resolved</returns>
        [HttpPost("getRawData")]
        [ProducesResponseType(typeof(RawEventsResult), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public ActionResult<RawEventsResult> GetRawData([FromBody] RawEventsOptions options)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var locations = GetRequestedLocations(options);
            if (locations.Count == 0)
                return BadRequest("Select at least one location");
            if (locations.Count > MaxLocations)
                return BadRequest($"Select no more than {MaxLocations} locations");

            if (options.End < options.Start)
                return BadRequest("End must be after Start");

            TimeOnly? dailyStart = null;
            TimeOnly? dailyEnd = null;
            var hasStart = !string.IsNullOrWhiteSpace(options.DailyStartTime);
            var hasEnd = !string.IsNullOrWhiteSpace(options.DailyEndTime);
            if (hasStart != hasEnd)
                return BadRequest("Provide both DailyStartTime and DailyEndTime, or neither");
            if (hasStart)
            {
                if (!TryParseTime(options.DailyStartTime!, out var parsedStart) || !TryParseTime(options.DailyEndTime!, out var parsedEnd))
                    return BadRequest("Daily times must look like HH:mm");
                if (parsedStart == parsedEnd)
                    return BadRequest("Daily start and end times must be different. Leave both empty for the whole day.");
                dailyStart = parsedStart;
                dailyEnd = parsedEnd;
            }

            try
            {
                var maxRows = Math.Clamp(options.MaxRows ?? DefaultMaxRows, 1, MaxAllowedRows);
                var rows = new List<RawEventDto>();
                var totalCount = 0;

                foreach (var location in locations)
                {
                    IEnumerable<IndianaEvent> events = _eventLogRepository
                        .GetEventsBetweenDates(location, options.Start, options.End);

                    if (dailyStart.HasValue && dailyEnd.HasValue)
                        events = events.Where(x => InDailyWindow(TimeOnly.FromDateTime(x.Timestamp), dailyStart.Value, dailyEnd.Value));

                    if (options.EventCodes?.Count > 0)
                        events = events.Where(x => options.EventCodes.Contains(x.EventCode));

                    if (options.EventParams?.Count > 0)
                        events = events.Where(x => options.EventParams.Contains(x.EventParam));

                    foreach (var evt in events.OrderBy(x => x.Timestamp))
                    {
                        totalCount++;
                        if (rows.Count >= maxRows)
                            continue;

                        rows.Add(new RawEventDto
                        {
                            LocationIdentifier = evt.LocationIdentifier,
                            EventCode = evt.EventCode,
                            EventCodeName = IndianaEventCodeCatalog.GetName(evt.EventCode),
                            EventParam = evt.EventParam,
                            Timestamp = evt.Timestamp
                        });
                    }
                }

                return Ok(new RawEventsResult
                {
                    Events = rows,
                    TotalCount = totalCount,
                    Truncated = totalCount > rows.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Raw events request failed for {Count} location(s)", locations.Count);
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Get every known event code with its readable name, category and what its
        /// parameter means. Used to fill the event code picker.
        /// </summary>
        [HttpGet("getEventCodes")]
        [ProducesResponseType(typeof(IEnumerable<EventCodeInfo>), StatusCodes.Status200OK)]
        public ActionResult<IEnumerable<EventCodeInfo>> GetEventCodes()
        {
            return Ok(IndianaEventCodeCatalog.All);
        }

        private static List<string> GetRequestedLocations(RawEventsOptions options)
        {
            var locations = new List<string>();
            if (!string.IsNullOrWhiteSpace(options.LocationIdentifier))
                locations.Add(options.LocationIdentifier.Trim());
            if (options.LocationIdentifiers != null)
                locations.AddRange(options.LocationIdentifiers.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));

            return locations.Distinct().ToList();
        }

        private static bool TryParseTime(string value, out TimeOnly time)
        {
            return TimeOnly.TryParseExact(value.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
        }

        /// <summary>
        /// True when <paramref name="time"/> is at or after <paramref name="start"/> and before
        /// <paramref name="end"/>. A window whose start is later than its end runs past midnight.
        /// </summary>
        private static bool InDailyWindow(TimeOnly time, TimeOnly start, TimeOnly end)
        {
            return start < end
                ? time >= start && time < end
                : time >= start || time < end;
        }
    }

    /// <summary>
    /// Request for raw event log records
    /// </summary>
    public class RawEventsOptions
    {
        /// <summary>A single location (signal) identifier. Kept for the chart Raw Data view.</summary>
        public string? LocationIdentifier { get; set; }

        /// <summary>Several location (signal) identifiers. Combined with <see cref="LocationIdentifier"/> when both are set.</summary>
        public List<string>? LocationIdentifiers { get; set; }

        /// <summary>Start of the time range</summary>
        public DateTime Start { get; set; }

        /// <summary>End of the time range</summary>
        public DateTime End { get; set; }

        /// <summary>
        /// Start of a time-of-day window applied to every day in the range, as HH:mm.
        /// Requires <see cref="DailyEndTime"/>. A start later than the end runs past midnight.
        /// </summary>
        public string? DailyStartTime { get; set; }

        /// <summary>End of the time-of-day window (exclusive), as HH:mm</summary>
        public string? DailyEndTime { get; set; }

        /// <summary>Only return these event codes. All codes when empty.</summary>
        public List<short>? EventCodes { get; set; }

        /// <summary>Only return these event parameters. All parameters when empty.</summary>
        public List<short>? EventParams { get; set; }

        /// <summary>Maximum rows to return. Defaults to 50,000 and is capped at 250,000.</summary>
        public int? MaxRows { get; set; }
    }

    /// <summary>
    /// Raw event log records
    /// </summary>
    public class RawEventsResult
    {
        /// <summary>Records ordered by location then timestamp</summary>
        public List<RawEventDto> Events { get; set; } = new();

        /// <summary>Number of matching records before the row limit was applied</summary>
        public int TotalCount { get; set; }

        /// <summary>True when more records matched than were returned</summary>
        public bool Truncated { get; set; }
    }

    /// <summary>
    /// A single event log record
    /// </summary>
    public class RawEventDto
    {
        /// <summary>Location (signal) identifier</summary>
        public string LocationIdentifier { get; set; } = string.Empty;

        /// <summary>Numeric event code</summary>
        public short EventCode { get; set; }

        /// <summary>Readable name of the event code</summary>
        public string EventCodeName { get; set; } = string.Empty;

        /// <summary>Event parameter, meaning depends on the event code</summary>
        public short EventParam { get; set; }

        /// <summary>When the event occurred</summary>
        public DateTime Timestamp { get; set; }
    }
}
