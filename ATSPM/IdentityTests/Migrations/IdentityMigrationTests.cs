using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Utah.Udot.Atspm.Data;
using Utah.Udot.Atspm.SqlDatabaseProvider;
using Xunit;

namespace Utah.Udot.ATSPM.IdentityTests.Migrations
{
    public class IdentityMigrationTests
    {
        [Fact]
        public void SqlServerProviderDiscoversAndGeneratesApiKeyMigration()
        {
            var options = new DbContextOptionsBuilder<IdentityContext>()
                .UseSqlServer("Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true",
                    sql => sql.MigrationsAssembly(SqlServerProvider.Migration))
                .Options;
            using var context = new IdentityContext(options);
            var migrations = context.GetService<IMigrationsAssembly>().Migrations;

            Assert.Contains("20260520185308_5_3", migrations.Keys);
            var migration = (Migration)Activator.CreateInstance(migrations["20260520185308_5_3"].AsType())!;
            Assert.NotNull(migration.TargetModel.FindEntityType("Utah.Udot.Atspm.Data.Models.IdentityModels.ApiKey"));

            var sql = context.GetService<IMigrator>().GenerateScript(
                "20250227162950_5_0", "20260520185308_5_3");
            Assert.Contains("CREATE TABLE [ApiKeys]", sql);
            Assert.Contains("CREATE TABLE [ApiKeyClaims]", sql);
        }
    }
}
