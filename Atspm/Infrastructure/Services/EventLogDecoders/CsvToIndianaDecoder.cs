#region license
// Copyright 2026 Utah Departement of Transportation
// for Infrastructure - Utah.Udot.Atspm.Infrastructure.Services.EventLogDecoders/CsvToIndianaDecoder.cs
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

using System.Text;
using Utah.Udot.Atspm.Data.Models.EventLogModels;

namespace Utah.Udot.Atspm.Infrastructure.Services.EventLogDecoders
{
    /// <summary>
    /// Decodes Frisco-format CSV event log files into <see cref="IndianaEvent"/> records.
    /// <para>
    /// The CSV format has 6 header lines followed by data rows. Each header line begins
    /// with a timestamp and two commas, then metadata (file path, intersection number,
    /// IP address, MAC address, log start time, phases in use). Data rows have the form:
    /// <c>Timestamp, EventCode, EventParam</c>
    /// </para>
    /// </summary>
    public class CsvToIndianaDecoder : EventLogDecoderBase<IndianaEvent>
    {
        private const int HeaderLineCount = 6;

        /// <inheritdoc/>
        /// <remarks>CSV files are plain text and never compressed.</remarks>
        public override bool IsCompressed(Stream stream) => false;

        /// <inheritdoc/>
        /// <remarks>CSV files are ASCII text with no high bytes.</remarks>
        public override bool IsEncoded(Stream stream) => false;

        /// <inheritdoc/>
        public override IEnumerable<IndianaEvent> Decode(Device device, Stream stream, CancellationToken cancelToken = default)
        {
            cancelToken.ThrowIfCancellationRequested();

            if (device == null)
                throw new ArgumentNullException(nameof(device), "Device can not be null");

            if (stream?.Length == 0)
                throw new InvalidDataException("Stream is empty");

            var locationIdentifier = device.Location.LocationIdentifier;

            HashSet<IndianaEvent> decodedLogs = new();

            try
            {
                stream.Position = 0;

                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

                // Skip the 6 header lines
                for (int i = 0; i < HeaderLineCount; i++)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (reader.ReadLine() == null)
                        throw new InvalidDataException($"CSV file has fewer than {HeaderLineCount} header lines");
                }

                // Parse data rows: Timestamp, EventCode, EventParam
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    cancelToken.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var parts = line.Split(',');

                    if (parts.Length < 3)
                        continue;

                    if (!DateTime.TryParse(parts[0].Trim(), out DateTime timestamp))
                        continue;

                    if (!short.TryParse(parts[1].Trim(), out short eventCode))
                        continue;

                    if (!short.TryParse(parts[2].Trim(), out short eventParam))
                        continue;

                    decodedLogs.Add(new IndianaEvent
                    {
                        LocationIdentifier = locationIdentifier,
                        Timestamp = timestamp,
                        EventCode = eventCode,
                        EventParam = eventParam
                    });
                }
            }
            catch (Exception e) when (e is not EventLogDecoderException)
            {
                throw new EventLogDecoderException(e);
            }

            return decodedLogs;
        }
    }
}
