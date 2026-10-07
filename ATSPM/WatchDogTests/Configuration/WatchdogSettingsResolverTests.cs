using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Utah.Udot.Atspm.Data;
using Utah.Udot.Atspm.Data.Models;
using Utah.Udot.Atspm.Infrastructure.Configuration;
using Utah.Udot.Atspm.Infrastructure.Services.WatchDogServices;
using Xunit;

namespace Utah.Udot.ATSPM.WatchDogTests.Configuration
{
    public class WatchdogSettingsResolverTests
    {
        [Fact]
        public async Task SavedValueWinsWhenNoConfigurationKeyIsPresent()
        {
            await using var db = CreateContext();
            db.WatchdogSettings.Add(new WatchdogSettingsRecord
            {
                Id = 1,
                SettingsJson = JsonSerializer.Serialize(new WatchdogConfiguration { MinimumRecords = 123 })
            });
            await db.SaveChangesAsync();

            var resolver = new WatchdogSettingsResolver(db, new ConfigurationBuilder().Build());
            var (effective, overrides) = await resolver.GetEffectiveAsync();

            Assert.Equal(123, effective.MinimumRecords);
            Assert.Empty(overrides);
        }

        [Fact]
        public async Task ExplicitConfigurationWinsEvenWhenEqualToCodeDefault()
        {
            await using var db = CreateContext();
            db.WatchdogSettings.Add(new WatchdogSettingsRecord
            {
                Id = 1,
                SettingsJson = JsonSerializer.Serialize(new WatchdogConfiguration { MinimumRecords = 123 })
            });
            await db.SaveChangesAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WatchdogConfiguration:MinimumRecords"] = "500"
            }).Build();

            var (effective, overrides) = await new WatchdogSettingsResolver(db, config).GetEffectiveAsync();

            Assert.Equal(500, effective.MinimumRecords);
            Assert.Contains("MinimumRecords", overrides);
        }

        private static ConfigContext CreateContext() => new(
            new DbContextOptionsBuilder<ConfigContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    }
}
