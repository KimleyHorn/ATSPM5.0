#region license
// Copyright 2026 Utah Departement of Transportation
// for Infrastructure - Utah.Udot.Atspm.Infrastructure.Services.HostedServices/DeviceEventLogHostedService.cs
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

using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Utah.Udot.Atspm.Data.Enums;
using Utah.Udot.Atspm.Infrastructure.Extensions;
using Utah.Udot.ATSPM.Infrastructure.Workflows;

namespace Utah.Udot.Atspm.Infrastructure.Services.HostedServices
{
    /// <summary>
    /// Hosted service for running the <see cref="DeviceEventLogWorkflow"/>
    /// </summary>
    /// <remarks>
    /// Hosted service for running the <see cref="DeviceEventLogWorkflow"/>
    /// </remarks>
    /// <param name="log"></param>
    /// <param name="serviceProvider"></param>
    /// <param name="options"></param>
    public class DeviceEventLogHostedService(ILogger<DeviceEventLogHostedService> log, IServiceScopeFactory serviceProvider, IOptions<DeviceEventLoggingConfiguration> options) : HostedServiceBase(log, serviceProvider)
    {
        private readonly IOptions<DeviceEventLoggingConfiguration> _options = options;

        /// <inheritdoc/>
        public override async Task Process(IServiceScope scope, Stopwatch stopwatch, CancellationToken cancellationToken = default)
        {
            var repo = scope.ServiceProvider.GetService<IDeviceRepository>();

            var workflow = new DeviceEventLogWorkflow(scope.ServiceProvider.GetService<IServiceScopeFactory>(), _options.Value.BatchSize, _options.Value.ParallelProcesses, cancellationToken);
            // WorkflowBase constructor already calls BeginInit(), which runs Initialize() in the background.
            // Calling BeginInit() or Initialize() again races with it, causing LinkSteps() to run twice and
            // the BroadcastBlock input to deliver each item twice. Just wait for the background initialization.
            await WaitForInitializedAsync(workflow, cancellationToken);

            if (workflow.Input == null)
                throw new InvalidOperationException("DeviceEventLogWorkflow.Input is null after construction — WorkflowBase.Initialize() did not run.");

            bool anyCsvDevices = false;

            await foreach (var d in repo.GetDevicesForLogging(_options.Value.DeviceEventLoggingQueryOptions))
            {
                if (d.DeviceConfiguration?.Protocol == TransportProtocols.Csv)
                {
                    anyCsvDevices = true;
                }
                else
                {
                    if (workflow.Input == null)
                        throw new InvalidOperationException($"DeviceEventLogWorkflow.Input became null during enumeration while processing device {d.DeviceIdentifier}.");

                    await workflow.Input.SendAsync(d);
                }
            }

            workflow.Input.Complete();

            await Task.WhenAll(workflow.Steps.Select(s => s.Completion));

            if (anyCsvDevices)
            {
                await ProcessCsvDevices(scope, repo, cancellationToken);
            }
        }

        /// <summary>
        /// Scans <see cref="DeviceEventLoggingConfiguration.CsvPath"/> for <c>*.csv</c> files,
        /// reads the intersection number from each file's header line 2, validates it exists in the
        /// Locations table, looks up the matching <see cref="Device"/>, and groups the files by device.
        /// Each device's files are then sent as <c>Tuple&lt;Device, FileInfo&gt;</c> into their own
        /// <see cref="DecodeEventLogWorkflow"/> so memory is released between intersections.
        /// </summary>
        private async Task ProcessCsvDevices(IServiceScope scope, IDeviceRepository repo, CancellationToken cancellationToken)
        {
            var locationRepo = scope.ServiceProvider.GetService<ILocationRepository>();
            var scopeFactory = scope.ServiceProvider.GetService<IServiceScopeFactory>();
            var batchSize = _options.Value.BatchSize > 0 ? _options.Value.BatchSize : 50000;
            var csvPath = _options.Value.CsvPath;
            var dir = new DirectoryInfo(csvPath);

            if (!dir.Exists)
            {
                log.LogWarning("CSV directory does not exist or is not accessible: {CsvPath}", csvPath);
                return;
            }

            var files = dir.GetFiles("*.csv", SearchOption.AllDirectories);
            log.LogInformation("Found {FileCount} CSV file(s) in {CsvPath}", files.Length, csvPath);

            // Load all CSV-protocol devices once for matching
            var csvDevices = repo.GetList()
                .Where(d => d.LoggingEnabled && d.DeviceConfiguration != null && d.DeviceConfiguration.Protocol == TransportProtocols.Csv)
                .ToList();

            log.LogInformation("Found {DeviceCount} CSV-protocol device(s) in database", csvDevices.Count);

            // Issues found during the run, logged as a summary at the end
            var issues = new List<(string Signal, string FileName, string Issue)>();

            // Pass 1: group files by device (only reads the header of each file)
            var filesByDevice = new Dictionary<Device, List<FileInfo>>();
            var locationExistsCache = new Dictionary<string, bool>();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var intersectionId = ReadIntersectionIdFromCsvHeader(file);

                if (intersectionId == null)
                {
                    log.LogWarning("Could not parse intersection ID from CSV header: {FileName}", file.Name);
                    issues.Add(("Unknown", file.Name, "Could not parse intersection ID from CSV header"));
                    continue;
                }

                // Check if the location exists in the database (cached so it's one lookup per intersection)
                if (!locationExistsCache.TryGetValue(intersectionId, out var locationExists))
                {
                    locationExists = await locationRepo.LocationExists(intersectionId);
                    locationExistsCache[intersectionId] = locationExists;
                }

                if (!locationExists)
                {
                    log.LogWarning("Location with ID '{IntersectionId}' does not exist in database. Skipping file {FileName}", intersectionId, file.Name);
                    issues.Add((intersectionId, file.Name, "Location does not exist in database"));
                    continue;
                }

                var device = csvDevices.FirstOrDefault(d => d.DeviceIdentifier == intersectionId);

                if (device == null)
                {
                    log.LogWarning("No matching device found for intersection ID '{IntersectionId}' from file {FileName}", intersectionId, file.Name);
                    issues.Add((intersectionId, file.Name, "No matching CSV device found"));
                    continue;
                }

                if (!filesByDevice.TryGetValue(device, out var deviceFiles))
                    filesByDevice[device] = deviceFiles = new List<FileInfo>();

                deviceFiles.Add(file);
            }

            log.LogInformation("Grouped {QueuedCount} of {FileCount} CSV file(s) into {DeviceCount} intersection(s)",
                filesByDevice.Values.Sum(v => v.Count), files.Length, filesByDevice.Count);

            // Pass 2: one workflow per intersection so memory is released between them
            foreach (var (device, deviceFiles) in filesByDevice)
            {
                cancellationToken.ThrowIfCancellationRequested();

                log.LogInformation("Processing {FileCount} CSV file(s) for device {DeviceIdentifier}", deviceFiles.Count, device.DeviceIdentifier);

                // Constructor already starts initialization, so only wait for it (see Process)
                var csvWorkflow = new DecodeEventLogWorkflow(scopeFactory, batchSize, cancellationToken);
                await WaitForInitializedAsync(csvWorkflow, cancellationToken);

                foreach (var file in deviceFiles)
                {
                    log.LogDebug("Queuing file {FileName} for device {DeviceIdentifier}", file.Name, device.DeviceIdentifier);
                    await csvWorkflow.Input.SendAsync(Tuple.Create(device, file));
                }

                csvWorkflow.Input.Complete();
                await Task.WhenAll(csvWorkflow.Steps.Select(s => s.Completion));

                // Files that could not be decoded are skipped and kept
                var failedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var failure in csvWorkflow.DecodeDeviceData.Failures)
                {
                    failedFiles.Add(failure.Item2.FullName);
                    issues.Add((device.DeviceIdentifier, failure.Item2.Name, failure.Item3));
                }

                var failedCount = csvWorkflow.SaveEventsToRepo.FailedCount;

                if (failedCount > 0)
                {
                    log.LogWarning("{FailedCount} save(s) failed for device {DeviceIdentifier}. Keeping its {FileCount} CSV file(s) so they are retried next run",
                        failedCount, device.DeviceIdentifier, deviceFiles.Count);
                    issues.Add((device.DeviceIdentifier, "All files", $"{failedCount} save(s) failed. Files kept to retry next run"));
                    continue;
                }

                // Delete this intersection's decoded source files only after all of its data saved
                if (_options.Value.DeleteCsvSource)
                {
                    foreach (var file in deviceFiles.Where(f => !failedFiles.Contains(f.FullName)))
                    {
                        try { file.Delete(); }
                        catch (Exception ex)
                        {
                            log.LogWarning(ex, "Could not delete CSV file {FileName}", file.FullName);
                            issues.Add((device.DeviceIdentifier, file.Name, $"Could not delete file: {ex.Message}"));
                        }
                    }
                }
            }

            // Summary of everything that was skipped or failed this run
            if (issues.Count == 0)
            {
                log.LogInformation("CSV import finished with no issues");
                return;
            }

            log.LogWarning("CSV import finished with {IssueCount} issue(s):", issues.Count);

            foreach (var (signal, fileName, issue) in issues.OrderBy(i => i.Signal).ThenBy(i => i.FileName))
            {
                log.LogWarning("CSV import issue - Signal {Signal} | {FileName} | {Issue}", signal, fileName, issue);
            }
        }

        /// <summary>
        /// Waits for a workflow's background initialization (started by the <c>WorkflowBase</c> constructor
        /// via <c>BeginInit()</c>) to complete. Calling <c>BeginInit()</c> or <c>Initialize()</c> explicitly a
        /// second time races with the background task and causes <c>LinkSteps()</c> to execute twice, resulting
        /// in the BroadcastBlock delivering each item to downstream steps twice.
        /// </summary>
        private static async Task WaitForInitializedAsync(Utah.Udot.NetStandardToolkit.BaseClasses.ServiceObjectBase workflow, CancellationToken ct)
        {
            if (workflow.IsInitialized) return;

            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler onInit = null;
            onInit = (_, _) => { workflow.Initialized -= onInit; tcs.TrySetResult(); };
            workflow.Initialized += onInit;

            // Re-check after subscribing to avoid a missed-event race
            if (workflow.IsInitialized)
            {
                workflow.Initialized -= onInit;
                return;
            }

            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
        }

        /// <summary>
        /// Reads header line 2 of a Frisco CSV file to extract the intersection number.
        /// Expected format: <c>timestamp,,Intersection#,601</c>
        /// Returns the trimmed number string, or <see langword="null"/> if it cannot be parsed.
        /// </summary>
        private static string ReadIntersectionIdFromCsvHeader(FileInfo file)
        {
            try
            {
                using var reader = file.OpenText();
                reader.ReadLine(); // line 1 — file path, skip
                var line = reader.ReadLine(); // line 2 — Intersection#
                if (line == null) return null;

                var parts = line.Split(',');
                // parts[0]=timestamp  parts[1]=""  parts[2]="Intersection#"  parts[3]="601"
                if (parts.Length >= 4 && parts[2].Trim().Equals("Intersection#", StringComparison.OrdinalIgnoreCase))
                    return parts[3].Trim();

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
