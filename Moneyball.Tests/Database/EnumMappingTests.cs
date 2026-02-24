using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moneyball.Core.Enums;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.Tests.Database
{
    public class EnumMappingTests
    {
        [LocalFact]
        public async Task SportType_Enum_Matches_Database_SportIds()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory) // Ensures it finds the copied file
                .AddJsonFile("appsettings.json")
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<MoneyballDbContext>();
            optionsBuilder.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));

            await using var context = new MoneyballDbContext(optionsBuilder.Options);

            var dbSports = await context.Sports.ToDictionaryAsync(s => s.SportId, s => s.Name, cancellationToken: TestContext.Current.CancellationToken);

            foreach (var enumValue in Enum.GetValues<SportType>())
            {
                if (enumValue == SportType.Unknown) continue;

                var enumId = (int)enumValue; // Cast to get its integer value (e.g., 1 for NBA)

                dbSports.Should().ContainKey(enumId, because: $"DB is missing ID {enumId} for {enumValue}");
                dbSports[enumId].Should().Be(enumValue, because: $"DB value for ID {enumId} should match enum name");
            }
        }
    }
}
