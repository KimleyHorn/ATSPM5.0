using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Utah.Udot.Atspm.Data;

namespace Utah.Udot.ATSPM.SqlDatabaseProvider.Migrations
{
    [DbContext(typeof(ConfigContext))]
    [Migration("20261007000000_WatchdogSettings")]
    public class WatchdogSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WatchdogSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SettingsJson = table.Column<string>(type: "varchar(max)", unicode: false, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_WatchdogSettings", x => x.Id));

            migrationBuilder.Sql("INSERT INTO [WatchdogSettings] ([Id], [SettingsJson], [UpdatedAt]) " +
                "VALUES (1, '{\"ConsecutiveCount\":3,\"MinPhaseTerminations\":50,\"PercentThreshold\":0.9," +
                "\"AmStartHour\":1,\"AmEndHour\":5,\"PmPeakStartHour\":16,\"PmPeakEndHour\":19," +
                "\"MinimumRecords\":500,\"WeekdayOnly\":false,\"LowHitThreshold\":50," +
                "\"MaximumPedestrianEvents\":25,\"EmailAllErrors\":true," +
                "\"DefaultEmailAddress\":\"SPMWatchdog@friscotexas.gov\"}', SYSDATETIMEOFFSET())");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.DropTable("WatchdogSettings");
    }
}
