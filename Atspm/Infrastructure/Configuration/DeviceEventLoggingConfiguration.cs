#region license
// Copyright 2026 Utah Departement of Transportation
// for Infrastructure - Utah.Udot.Atspm.Infrastructure.Configuration/DeviceEventLoggingConfiguration.cs
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

using Utah.Udot.Atspm.Data.Models.EventLogModels;

namespace Utah.Udot.Atspm.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration options for device event logging
    /// </summary>
    public class DeviceEventLoggingConfiguration
    {
        /// <summary>
        /// Path to local directory where event logs are saved
        /// </summary>
        public string Path { get; set; } = System.IO.Path.GetTempPath();

        /// <summary>
        /// Path to local directory where Frisco CSV event log files are deposited.
        /// Only used when devices with <see cref="Utah.Udot.Atspm.Data.Enums.TransportProtocols.Csv"/>
        /// are included in the logging run.
        /// </summary>
        public string CsvPath { get; set; } = System.IO.Path.GetTempPath();

        /// <summary>
        /// When <see langword="true"/>, each CSV source file is deleted after it has been
        /// successfully imported. Set to <see langword="false"/> to keep files on disk.
        /// </summary>
        public bool DeleteCsvSource { get; set; } = true;

        /// <summary>
        /// Batch size of <see cref="EventLogModelBase"/> objects when saving to repository
        /// </summary>
        public int BatchSize { get; set; }

        /// <summary>
        /// Amount of processes that can be run in parallel
        /// </summary>
        public int ParallelProcesses { get; set; }

        /// <inheritdoc cref="DeviceEventLoggingQueryOptions"/>
        public DeviceEventLoggingQueryOptions DeviceEventLoggingQueryOptions { get; set; } = new();
    }
}
