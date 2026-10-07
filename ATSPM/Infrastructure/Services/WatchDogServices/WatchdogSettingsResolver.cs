using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Utah.Udot.Atspm.Data;
using Utah.Udot.Atspm.Data.Models;
using Utah.Udot.Atspm.Infrastructure.Configuration;

namespace Utah.Udot.Atspm.Infrastructure.Services.WatchDogServices
{
    /// <summary>Resolves code defaults, saved settings, then explicitly supplied configuration keys.</summary>
    public class WatchdogSettingsResolver(ConfigContext context, IConfiguration configuration)
    {
        public async Task<WatchdogConfiguration> GetSavedAsync(CancellationToken cancellationToken = default)
        {
            var record = await context.WatchdogSettings.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
            return record == null
                ? new WatchdogConfiguration()
                : JsonSerializer.Deserialize<WatchdogConfiguration>(record.SettingsJson) ?? new WatchdogConfiguration();
        }

        public async Task<(WatchdogConfiguration Effective, string[] Overrides)> GetEffectiveAsync(CancellationToken cancellationToken = default)
        {
            var effective = await GetSavedAsync(cancellationToken);
            var section = configuration.GetSection(nameof(WatchdogConfiguration));
            var propertyNames = typeof(WatchdogConfiguration).GetProperties()
                .Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var overrides = section.GetChildren().Select(x => x.Key)
                .Where(propertyNames.Contains).ToArray();
            section.Bind(effective); // Binder changes only keys actually present in configuration.
            return (effective, overrides);
        }

        public async Task SaveAsync(WatchdogConfiguration settings, CancellationToken cancellationToken = default)
        {
            var record = await context.WatchdogSettings.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
            if (record == null)
            {
                record = new WatchdogSettingsRecord { Id = 1 };
                context.WatchdogSettings.Add(record);
            }
            record.SettingsJson = JsonSerializer.Serialize(settings);
            record.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
