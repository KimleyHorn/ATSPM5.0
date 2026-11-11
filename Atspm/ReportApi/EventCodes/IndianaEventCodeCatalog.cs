#region license
// Copyright 2026 Utah Departement of Transportation
// for ReportApi - Utah.Udot.Atspm.ReportApi.EventCodes/IndianaEventCodeCatalog.cs
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

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Utah.Udot.Atspm.Data.Enums;

namespace Utah.Udot.Atspm.ReportApi.EventCodes
{
    /// <summary>
    /// Information about an Indiana event code
    /// </summary>
    public class EventCodeInfo
    {
        /// <summary>Numeric event code</summary>
        public ushort Code { get; set; }

        /// <summary>Readable name, from the enumeration's display name</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Event category, such as DetectorEvents. Empty when the enumeration has none.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>What the event parameter means for this code, such as PhaseNumber</summary>
        public string ParamType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Lookup of event code names, categories and parameter types built once from
    /// <see cref="IndianaEnumerations"/>.
    /// </summary>
    public static class IndianaEventCodeCatalog
    {
        private static readonly Lazy<IReadOnlyDictionary<ushort, EventCodeInfo>> _codes = new(Build);

        /// <summary>All known event codes, ordered by code</summary>
        public static IReadOnlyCollection<EventCodeInfo> All => _codes.Value.Values.OrderBy(c => c.Code).ToList();

        /// <summary>
        /// Readable name for an event code, or <c>Unknown (n)</c> when the code is not defined
        /// </summary>
        public static string GetName(short eventCode)
        {
            // Event codes are stored as shorts but the enumeration is ushort-based.
            var code = unchecked((ushort)eventCode);
            return _codes.Value.TryGetValue(code, out var info) ? info.Name : $"Unknown ({eventCode})";
        }

        private static IReadOnlyDictionary<ushort, EventCodeInfo> Build()
        {
            var result = new Dictionary<ushort, EventCodeInfo>();

            foreach (var field in typeof(IndianaEnumerations).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                var code = Convert.ToUInt16(field.GetRawConstantValue());
                var layer = field.GetCustomAttributes<IndianaEventLayerAttribute>().FirstOrDefault();

                // Some codes share a value; keep the first definition
                result.TryAdd(code, new EventCodeInfo
                {
                    Code = code,
                    Name = field.GetCustomAttribute<DisplayAttribute>()?.Name ?? field.Name,
                    Category = layer != null && layer.IndianaEventCategory != IndianaEventCategory.none
                        ? layer.IndianaEventCategory.ToString()
                        : string.Empty,
                    ParamType = layer != null && layer.IndianaEventParamType != IndianaEventParamType.None
                        ? layer.IndianaEventParamType.ToString()
                        : string.Empty
                });
            }

            return result;
        }
    }
}
