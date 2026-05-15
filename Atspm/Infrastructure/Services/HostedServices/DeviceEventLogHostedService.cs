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
            // WorkflowBase constructor calls BeginInit() which runs Initialize() in the background.
            // Calling Initialize() explicitly again would race with the background task, causing
            // LinkSteps() to execute twice and BroadcastBlock to deliver each item twice.
            // Instead, wait for the background initialization to complete.
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
                var csvWorkflow = new DecodeEventLogWorkflow(scope.ServiceProvider.GetService<IServiceScopeFactory>(), _options.Value.BatchSize > 0 ? _options.Value.BatchSize : 50000, cancellationToken);
                await WaitForInitializedAsync(csvWorkflow, cancellationToken);
                await ProcessCsvDevices(scope, repo, csvWorkflow, cancellationToken);
            }
        }

        /// <summary>
        /// Scans <see cref="DeviceEventLoggingConfiguration.CsvPath"/> for <c>*.csv</c> files,
        /// reads the intersection number from each file's header line 2, looks up the matching
        /// <see cref="Device"/>, and sends <c>Tuple&lt;Device, FileInfo&gt;</c> into the
        /// <see cref="DecodeEventLogWorkflow"/> pipeline.
        /// </summary>
        private async Task ProcessCsvDevices(IServiceScope scope, IDeviceRepository repo, DecodeEventLogWorkflow csvWorkflow, CancellationToken cancellationToken)
        {
            var csvPath = _options.Value.CsvPath;
            var dir = new DirectoryInfo(csvPath);

            if (!dir.Exists)
                return;

            var files = dir.GetFiles("*.csv", SearchOption.AllDirectories);

            // Load all CSV-protocol devices once for matching
            var csvDevices = repo.GetList()
                .Where(d => d.LoggingEnabled && d.DeviceConfiguration != null && d.DeviceConfiguration.Protocol == TransportProtocols.Csv)
                .ToList();

            // Track files that were successfully queued so we can delete them AFTER the workflow finishes
            var queuedFiles = new List<FileInfo>();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var intersectionId = ReadIntersectionIdFromCsvHeader(file);

                if (intersectionId == null)
                    continue;

                var device = csvDevices.FirstOrDefault(d => d.DeviceIdentifier == intersectionId);

                if (device == null)
                    continue;

                await csvWorkflow.Input.SendAsync(Tuple.Create(device, file));

                queuedFiles.Add(file);
            }

            csvWorkflow.Input.Complete();
            await Task.WhenAll(csvWorkflow.Steps.Select(s => s.Completion));

            // Delete source files only after the workflow has fully completed
            if (_options.Value.DeleteCsvSource)
            {
                foreach (var file in queuedFiles)
                {
                    try { file.Delete(); }
                    catch { /* non-fatal: file may already be gone or locked */ }
                }
            }
        }

        /// <summary>
        /// Waits for a workflow's background initialization (started by the WorkflowBase constructor
        /// via BeginInit) to complete. Calling <c>Initialize()</c> explicitly a second time races
        /// with the background task and causes <c>LinkSteps()</c> to execute twice, resulting in the
        /// BroadcastBlock delivering each item to downstream steps twice.
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
