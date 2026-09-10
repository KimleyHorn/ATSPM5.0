#region license
// Copyright 2026 Utah Departement of Transportation
// for Infrastructure - Utah.Udot.Atspm.Infrastructure.Configuration/DeleteEventsConfiguration.cs
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

namespace Utah.Udot.Atspm.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration for deleting expired compressed event log data
    /// </summary>
    public class DeleteEventsConfiguration
    {
        /// <summary>
        /// Number of days of event data to retain (older data will be deleted)
        /// </summary>
        public int DaysToRetain { get; set; } = 30;

        /// <summary>
        /// If true, logs what would be deleted without actually deleting
        /// </summary>
        public bool IsDryRun { get; set; } = false;
    }
}
