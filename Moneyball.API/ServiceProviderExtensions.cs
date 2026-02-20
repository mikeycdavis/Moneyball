using Microsoft.EntityFrameworkCore;
using Moneyball.Infrastructure.Repositories;

namespace Moneyball.API;

public static class ServiceProviderExtensions
{
    extension(IServiceProvider services)
    {
        public void MigrateDatabase()
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MoneyballDbContext>();

            try
            {
                var pendingMigrations = db.Database.GetPendingMigrations();

                if (!pendingMigrations.Any())
                    return;

                db.Database.Migrate();
                Console.WriteLine("Database migration completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during database migration: {ex.Message}");
            }
        }

    }
}