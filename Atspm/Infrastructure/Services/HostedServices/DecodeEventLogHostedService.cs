#region license
// Copyright 2026 Utah Departement of Transportation
// for Infrastructure - Utah.Udot.Atspm.Infrastructure.Services.HostedServices/DecodeEventLogHostedService.cs
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
using Utah.Udot.ATSPM.Infrastructure.Workflows;

namespace Utah.Udot.Atspm.Infrastructure.Services.HostedServices
{
    public class DecodeEventLogHostedService(ILogger<DecodeEventLogHostedService> log, IServiceScopeFactory serviceProvider, IOptions<DecodeEventsConfiguration> options) : HostedServiceBase(log, serviceProvider)
    {
        private readonly IOptions<DecodeEventsConfiguration> _options = options;

        /// <inheritdoc/>
        public override async Task Process(IServiceScope scope, Stopwatch stopwatch, CancellationToken cancellationToken = default)
        {
            var repo = scope.ServiceProvider.GetService<IDeviceRepository>();

            var workflow = new DecodeEventLogWorkflow(scope.ServiceProvider.GetService<IServiceScopeFactory>(), 50000, cancellationToken);

            Console.WriteLine($"path: {_options.Value.Path}");

            var dir = new DirectoryInfo(_options.Value.Path);

            // Files that were queued for decoding, and issues found during the run (logged as a summary at the end)
            var queuedFiles = new List<FileInfo>();
            var issues = new List<(string Folder, string Issue)>();

            if (dir.Exists)
            {
                var files = dir.GetFiles("*", SearchOption.AllDirectories);

                Console.WriteLine($"file count {files.Length}");

                var groups = files.GroupBy(f => f.Directory.Name);

                foreach (var g in groups)
                {
                    // Supported folder layouts:
                    //   MainFolder\{SignalId}\*.dat   - folder named only with the signal id (only .dat/.datZ files are read)
                    //   MainFolder\{SignalId}-{Ip}\*  - original layout, every file is read
                    var device = repo.GetList().FirstOrDefault(f => f.DeviceIdentifier == g.Key);
                    var isSignalIdFolder = device != null;
                    IEnumerable<FileInfo> folderFiles = g;

                    if (device == null && g.Key.Contains('-'))
                    {
                        var id = g.Key.Split('-')[0];
                        device = repo.GetList().FirstOrDefault(f => f.DeviceIdentifier == id);
                    }

                    if (device == null)
                    {
                        // Folders with no matching device are skipped, except the root folder itself
                        if (!string.Equals(g.Key, dir.Name, StringComparison.OrdinalIgnoreCase))
                            issues.Add((g.Key, "No matching device found for folder name"));
                        continue;
                    }

                    if (isSignalIdFolder)
                    {
                        folderFiles = g.Where(f =>
                            f.Extension.Equals(".dat", StringComparison.OrdinalIgnoreCase) ||
                            f.Extension.Equals(".datz", StringComparison.OrdinalIgnoreCase));
                    }

                    foreach (var f in folderFiles)
                    {
                        queuedFiles.Add(f);
                        await workflow.Input.SendAsync(Tuple.Create(device, f));
                    }
                }
            }
            else
            {
                Console.WriteLine($"directory {_options.Value.Path} doesn't exist");
            }

            workflow.Input.Complete();

            await Task.WhenAll(workflow.Steps.Select(s => s.Completion));

            // Files that could not be decoded are skipped and kept
            var failedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var failure in workflow.DecodeDeviceData.Failures)
            {
                failedFiles.Add(failure.Item2.FullName);
                issues.Add((failure.Item1.DeviceIdentifier, $"{failure.Item2.Name}: {failure.Item3}"));
            }

            var failedSaves = workflow.SaveEventsToRepo.FailedCount;

            if (failedSaves > 0)
            {
                log.LogWarning("{FailedCount} save(s) failed. Keeping all source files so they are retried next run", failedSaves);
                issues.Add(("All files", $"{failedSaves} save(s) failed. Files kept to retry next run"));
            }
            else if (_options.Value.DeleteSource)
            {
                // Delete only after everything saved successfully, and never delete files that failed to decode
                foreach (var file in queuedFiles.Where(f => !failedFiles.Contains(f.FullName)))
                {
                    try { file.Delete(); }
                    catch (Exception ex)
                    {
                        log.LogWarning(ex, "Could not delete file {FileName}", file.FullName);
                        issues.Add((file.Directory?.Name ?? "Unknown", $"{file.Name}: could not delete file ({ex.Message})"));
                    }
                }
            }

            log.LogInformation("Decode events finished. Queued {QueuedCount} file(s), {IssueCount} issue(s)", queuedFiles.Count, issues.Count);

            foreach (var (folder, issue) in issues.OrderBy(i => i.Folder))
            {
                log.LogWarning("Decode events issue - {Folder} | {Issue}", folder, issue);
            }
        }
    }
}