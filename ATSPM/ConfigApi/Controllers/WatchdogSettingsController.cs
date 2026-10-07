using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Utah.Udot.Atspm.Common;
using Utah.Udot.Atspm.Infrastructure.Attributes;
using Utah.Udot.Atspm.Infrastructure.Configuration;
using Utah.Udot.Atspm.Infrastructure.Services.WatchDogServices;

namespace Utah.Udot.Atspm.ConfigApi.Controllers
{
    [ApiController]
    [ApiVersion(1.0)]
    [Route("api/v{version:apiVersion}/WatchdogSettings")]
    public class WatchdogSettingsController(WatchdogSettingsResolver resolver) : ControllerBase
    {
        [HttpGet]
        [AuthorizePermission(AtspmAuthorization.Permissions.GeneralConfigurationsView)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var saved = await resolver.GetSavedAsync(cancellationToken);
            var (effective, overrides) = await resolver.GetEffectiveAsync(cancellationToken);
            return Ok(new { saved, effective, overrides });
        }

        [HttpGet("time-zones")]
        [AuthorizePermission(AtspmAuthorization.Permissions.GeneralConfigurationsView)]
        public IActionResult GetTimeZones() => Ok(WatchdogTimeZones.GetAvailable());

        [HttpPut]
        [AuthorizePermission(AtspmAuthorization.Permissions.GeneralConfigurationsEdit)]
        public async Task<IActionResult> Put([FromBody] WatchdogConfiguration settings, CancellationToken cancellationToken)
        {
            if (settings == null) return BadRequest("Settings are required.");
            if (!WatchdogTimeZones.IsValid(settings.TimeZoneId))
                return BadRequest("Select a valid time zone.");
            if (!ValidHours(settings) || settings.PercentThreshold is < 0 or > 1 ||
                settings.ConsecutiveCount < 1 || settings.MinPhaseTerminations < 0 ||
                settings.MinimumRecords < 0 || settings.LowHitThreshold < 0 ||
                settings.LowHitRampThreshold < 0 || settings.MaximumPedestrianEvents < 0 ||
                settings.RampMissedEventsThreshold < 0 ||
                (settings.PmPeakStartHour >= settings.PmPeakEndHour))
                return BadRequest("Check hour ranges, thresholds, and consecutive count.");

            await resolver.SaveAsync(settings, cancellationToken);
            var (effective, overrides) = await resolver.GetEffectiveAsync(cancellationToken);
            return Ok(new { saved = settings, effective, overrides });
        }

        private static bool ValidHours(WatchdogConfiguration settings)
        {
            var hours = typeof(WatchdogConfiguration).GetProperties()
                .Where(x => x.Name.EndsWith("Hour", StringComparison.Ordinal))
                .Select(x => (int)x.GetValue(settings)!);
            return hours.All(x => x is >= 0 and <= 23);
        }
    }
}
