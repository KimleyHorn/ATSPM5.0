#region license
// Copyright 2026 Utah Departement of Transportation
// for Infrastructure - Utah.Udot.Atspm.Infrastructure.Services.HostedServices/DeleteExpiredEventsHostedService.cs
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Utah.Udot.Atspm.Infrastructure.Configuration;

namespace Utah.Udot.Atspm.Infrastructure.Services.HostedServices
{
    /// <summary>
    /// Hosted service for deleting expired compressed event log data
    /// </summary>
    /// <remarks>
    /// Deletes event log records from the CompressedEvents table where the 
    /// End date is older than the configured retention period.
    /// </remarks>
    /// <param name="log"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="options"></param>
    public class DeleteExpiredEventsHostedService(
        ILogger<DeleteExpiredEventsHostedService> log,
        IServiceScopeFactory serviceProvider,
        IOptions<DeleteEventsConfiguration> options)
        : HostedServiceBase(log, serviceProvider)
    {
        private readonly IOptions<DeleteEventsConfiguration> _options = options;

        /// <inheritdoc/>
        public override async Task Process(IServiceScope scope, Stopwatch stopwatch, CancellationToken cancellationToken = default)
        {
            var eventLogRepo = scope.ServiceProvider.GetService<IEventLogRepository>();

            if (eventLogRepo == null)
                throw new InvalidOperationException("IEventLogRepository is not registered in the service provider");

            var cutoffDate = DateTime.Now.AddDays(-_options.Value.DaysToRetain);

            log.LogInformation("Starting deletion of event logs older than {CutoffDate} ({DaysToRetain} days)",
                cutoffDate.Date, _options.Value.DaysToRetain);

            if (_options.Value.IsDryRun)
            {
                log.LogWarning("DRY RUN MODE - No data will be deleted");
            }

            try
            {
                // Get all expired records
                var allRecords = eventLogRepo.GetList();
                var expiredRecords = allRecords.Where(r => r.End < cutoffDate).ToList();

                log.LogInformation("Found {Count} expired record(s) to delete", expiredRecords.Count);

                if (expiredRecords.Count == 0)
                {
                    log.LogInformation("No records found older than {CutoffDate}", cutoffDate.Date);
                    return;
                }

                // Preview mode
                if (_options.Value.IsDryRun)
                {
                    foreach (var record in expiredRecords.Take(10))
                    {
                        log.LogInformation("Would delete: Location={Location}, Device={Device}, Period={Start}-{End}, Type={Type}",
                            record.LocationIdentifier, record.DeviceId, record.Start, record.End, record.DataType?.Name);
                    }

                    if (expiredRecords.Count > 10)
                    {
                        log.LogInformation("... and {Count} more record(s)", expiredRecords.Count - 10);
                    }

                    return;
                }

                // Actual deletion
                int deletedCount = 0;
                foreach (var record in expiredRecords)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        await eventLogRepo.RemoveAsync(record);
                        deletedCount++;

                        if (deletedCount % 100 == 0)
                        {
                            log.LogInformation("Deleted {Count} record(s)...", deletedCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Error deleting record for Location={Location}, Device={Device}",
                            record.LocationIdentifier, record.DeviceId);
                    }
                }

                log.LogInformation("Successfully deleted {DeletedCount} of {TotalCount} expired record(s) in {Elapsed}",
                    deletedCount, expiredRecords.Count, stopwatch.Elapsed);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Error during deletion process");
                throw;
            }
        }
    }
}
