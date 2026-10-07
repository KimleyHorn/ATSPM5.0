namespace Utah.Udot.Atspm.Infrastructure.Configuration
{
    /// <summary>Time zone identifiers accepted by the ConfigApi runtime.</summary>
    public static class WatchdogTimeZones
    {
        public static IReadOnlyList<TimeZoneOption> GetAvailable()
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
            {
                ids.Add(zone.Id);
                if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaId) &&
                    IsValid(ianaId))
                {
                    ids.Add(ianaId);
                }
            }

            return ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .Select(id => new TimeZoneOption(id, TimeZoneInfo.FindSystemTimeZoneById(id).DisplayName))
                .ToArray();
        }

        public static bool IsValid(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch (TimeZoneNotFoundException) { return false; }
            catch (InvalidTimeZoneException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }

    public record TimeZoneOption(string Id, string DisplayName);
}
