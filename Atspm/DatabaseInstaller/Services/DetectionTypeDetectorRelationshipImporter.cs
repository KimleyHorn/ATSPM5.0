#region license
// Copyright 2026 Utah Departement of Transportation
// for DatabaseInstaller - DatabaseInstaller.Services/DetectionTypeDetectorRelationshipImporter.cs
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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Utah.Udot.Atspm.Data;
using Utah.Udot.Atspm.Data.Enums;
using Utah.Udot.Atspm.Data.Models;

namespace DatabaseInstaller.Services;

public class DetectionTypeDetectorImportRow
{
    public int DetectionTypesId { get; set; }
    public int DetectorsId { get; set; }
}

public record DetectionTypeDetectorImportResult(
    int SourceRows,
    int AddedRelationships,
    int ExistingRelationships,
    int MissingDetectors,
    int MissingDetectionTypes,
    int ClearedRelationships);

public static class DetectionTypeDetectorRelationshipImporter
{
    public static async Task<DetectionTypeDetectorImportResult> ImportAsync(
        ConfigContext context,
        IReadOnlyCollection<DetectionTypeDetectorImportRow> sourceRows,
        ILogger logger,
        bool clearExisting,
        CancellationToken cancellationToken)
    {
        var detectionTypes = await context.DetectionTypes
            .ToDictionaryAsync(d => (int)d.Id, cancellationToken);

        var detectors = await context.Detectors
            .Include(d => d.DetectionTypes)
            .ToListAsync(cancellationToken);

        var detectorIds = detectors.Select(d => d.Id).ToHashSet();
        var missingDetectors = sourceRows.Count(r => !detectorIds.Contains(r.DetectorsId));
        var missingDetectionTypes = sourceRows.Count(r =>
            detectorIds.Contains(r.DetectorsId) &&
            !detectionTypes.ContainsKey(r.DetectionTypesId));

        var clearedRelationships = 0;
        if (clearExisting)
        {
            foreach (var detector in detectors)
            {
                clearedRelationships += detector.DetectionTypes.Count;
                detector.DetectionTypes.Clear();
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        var sourceDetectionTypeIdsByDetector = sourceRows
            .Where(r => detectorIds.Contains(r.DetectorsId))
            .Where(r => detectionTypes.ContainsKey(r.DetectionTypesId))
            .GroupBy(r => r.DetectorsId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(r => r.DetectionTypesId).Distinct().ToHashSet());

        var addedRelationships = 0;
        var existingRelationships = 0;

        foreach (var detector in detectors)
        {
            if (detectionTypes.TryGetValue((int)DetectionTypes.B, out var basicDetectionType))
            {
                AddDetectionType(detector, basicDetectionType, ref addedRelationships, ref existingRelationships);
            }

            if (!sourceDetectionTypeIdsByDetector.TryGetValue(detector.Id, out var detectionTypeIds))
            {
                continue;
            }

            foreach (var detectionTypeId in detectionTypeIds)
            {
                AddDetectionType(detector, detectionTypes[detectionTypeId], ref addedRelationships, ref existingRelationships);
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "DetectionTypeDetector import complete. Source rows: {SourceRows}. Added: {Added}. Existing: {Existing}. Missing detectors: {MissingDetectors}. Missing detection types: {MissingDetectionTypes}. Cleared: {Cleared}.",
            sourceRows.Count,
            addedRelationships,
            existingRelationships,
            missingDetectors,
            missingDetectionTypes,
            clearedRelationships);

        return new DetectionTypeDetectorImportResult(
            sourceRows.Count,
            addedRelationships,
            existingRelationships,
            missingDetectors,
            missingDetectionTypes,
            clearedRelationships);
    }

    private static void AddDetectionType(
        Detector detector,
        DetectionType detectionType,
        ref int addedRelationships,
        ref int existingRelationships)
    {
        if (detector.DetectionTypes.Any(d => d.Id == detectionType.Id))
        {
            existingRelationships++;
            return;
        }

        detector.DetectionTypes.Add(detectionType);
        addedRelationships++;
    }
}
