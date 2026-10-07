using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Utah.Udot.Atspm.Data;
using Utah.Udot.Atspm.SqlDatabaseProvider;
using Xunit;

namespace Utah.Udot.ATSPM.WatchDogTests.Configuration
{
    public class WatchdogMigrationTests
    {
        [Fact]
        public void SqlServerProviderDiscoversWatchdogSettingsMigration()
        {
            var options = new DbContextOptionsBuilder<ConfigContext>()
                .UseSqlServer("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true",
                    sql => sql.MigrationsAssembly(SqlServerProvider.Migration))
                .Options;
            using var context = new ConfigContext(options);

            var migrations = context.GetService<IMigrationsAssembly>().Migrations.Keys;
            Assert.Contains("20261007000000_WatchdogSettings", migrations);
            var previous = migrations.Where(x => string.CompareOrdinal(x, "20261007000000_WatchdogSettings") < 0)
                .OrderBy(x => x).Last();

            var sql = context.GetService<IMigrator>().GenerateScript(
                previous, "20261007000000_WatchdogSettings");
            Assert.Contains("CREATE TABLE [WatchdogSettings]", sql);
            Assert.Contains("INSERT INTO [WatchdogSettings]", sql);
        }
    }
}
