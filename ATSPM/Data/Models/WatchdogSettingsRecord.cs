namespace Utah.Udot.Atspm.Data.Models
{
    /// <summary>Singleton, JSON-backed WatchDog settings in the configuration database.</summary>
    public class WatchdogSettingsRecord
    {
        public int Id { get; set; } = 1;
        public string SettingsJson { get; set; } = "{}";
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
