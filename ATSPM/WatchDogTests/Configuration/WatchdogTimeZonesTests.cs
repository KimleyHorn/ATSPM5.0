using Utah.Udot.Atspm.Infrastructure.Configuration;
using Xunit;

namespace Utah.Udot.ATSPM.WatchDogTests.Configuration
{
    public class WatchdogTimeZonesTests
    {
        [Fact]
        public void ListedTimeZonesAreValidAndIncludeCurrentDefault()
        {
            var zones = WatchdogTimeZones.GetAvailable();

            Assert.Contains(zones, zone => zone.Id == WatchdogConfiguration.DefaultTimeZoneId);
            Assert.All(zones, zone => Assert.True(WatchdogTimeZones.IsValid(zone.Id)));
            Assert.False(WatchdogTimeZones.IsValid("Not/A_Time_Zone"));
        }
    }
}
