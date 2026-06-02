#region license
// Copyright 2026 Utah Departement of Transportation
// for DatabaseInstaller - DatabaseInstaller.Services/ImportDetectionTypeDetectorHostedService.cs
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

using DatabaseInstaller.Commands;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Utah.Udot.Atspm.Data;

namespace DatabaseInstaller.Services
{
    public class ImportDetectionTypeDetectorHostedService : IHostedService
    {
        private readonly ILogger<ImportDetectionTypeDetectorHostedService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ImportDetectionTypeDetectorCommandConfiguration _config;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public ImportDetectionTypeDetectorHostedService(
            ILogger<ImportDetectionTypeDetectorHostedService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            IOptions<ImportDetectionTypeDetectorCommandConfiguration> config,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _config = config.Value;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var query = GetDetectionTypeDetectorQuery();
                var columnMappings = GetDetectionTypeDetectorColumnMappings();

                _logger.LogInformation("Importing DetectionTypeDetector relationships from source.");
                var sourceRows = await ImportSourceRelationships(query, columnMappings, cancellationToken);
                _logger.LogInformation("Read {Count} DetectionTypeDetector source rows.", sourceRows.Count);

                using var scope = _serviceProvider.CreateScope();
                var configContext = scope.ServiceProvider.GetRequiredService<ConfigContext>();

                if (!string.IsNullOrWhiteSpace(_config.ConfigConnection))
                {
                    _logger.LogInformation("Overriding ConfigContext connection string.");
                    configContext.Database.SetConnectionString(_config.ConfigConnection);
                }

                await configContext.Database.OpenConnectionAsync(cancellationToken);
                await DetectionTypeDetectorRelationshipImporter.ImportAsync(
                    configContext,
                    sourceRows,
                    _logger,
                    _config.Clear,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DetectionTypeDetector relationship import failed.");
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private string GetDetectionTypeDetectorQuery()
        {
            var queries = _configuration.GetSection("LocationQueries").Get<Dictionary<string, string>>();
            if (queries == null || !queries.TryGetValue("DetectionTypeDetector", out var query) || string.IsNullOrWhiteSpace(query))
            {
                throw new KeyNotFoundException("LocationQueries:DetectionTypeDetector was not found in appsettings.");
            }

            return query;
        }

        private Dictionary<string, string> GetDetectionTypeDetectorColumnMappings()
        {
            var columnMappings = _configuration
                .GetSection("ColumnMappings:DetectionTypeDetector")
                .Get<Dictionary<string, string>>();

            if (columnMappings == null || columnMappings.Count == 0)
            {
                throw new KeyNotFoundException("ColumnMappings:DetectionTypeDetector was not found in appsettings.");
            }

            return columnMappings;
        }

        private async Task<List<DetectionTypeDetectorImportRow>> ImportSourceRelationships(
            string query,
            IReadOnlyDictionary<string, string> columnMappings,
            CancellationToken cancellationToken)
        {
            var detectionTypeColumn = GetColumnName(columnMappings, nameof(DetectionTypeDetectorImportRow.DetectionTypesId));
            var detectorColumn = GetColumnName(columnMappings, nameof(DetectionTypeDetectorImportRow.DetectorsId));
            var sourceRows = new List<DetectionTypeDetectorImportRow>();

            await using var sourceConnection = new SqlConnection(_config.Source);
            await sourceConnection.OpenAsync(cancellationToken);

            await using var sourceCommand = new SqlCommand(query, sourceConnection);
            await using var reader = await sourceCommand.ExecuteReaderAsync(cancellationToken);
            var detectionTypeOrdinal = reader.GetOrdinal(detectionTypeColumn);
            var detectorOrdinal = reader.GetOrdinal(detectorColumn);

            while (await reader.ReadAsync(cancellationToken))
            {
                if (await reader.IsDBNullAsync(detectionTypeOrdinal, cancellationToken) ||
                    await reader.IsDBNullAsync(detectorOrdinal, cancellationToken))
                {
                    continue;
                }

                sourceRows.Add(new DetectionTypeDetectorImportRow
                {
                    DetectionTypesId = Convert.ToInt32(reader.GetValue(detectionTypeOrdinal)),
                    DetectorsId = Convert.ToInt32(reader.GetValue(detectorOrdinal))
                });
            }

            return sourceRows;
        }

        private static string GetColumnName(IReadOnlyDictionary<string, string> columnMappings, string propertyName)
        {
            var column = columnMappings
                .FirstOrDefault(m => string.Equals(m.Value, propertyName, StringComparison.OrdinalIgnoreCase))
                .Key;

            if (string.IsNullOrWhiteSpace(column))
            {
                throw new KeyNotFoundException($"No source column is mapped to {propertyName}.");
            }

            return column;
        }
    }
}
