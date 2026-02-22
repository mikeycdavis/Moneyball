using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moneyball.Core.Interfaces.ExternalServices;

namespace Moneyball.Infrastructure.ExternalServices;

public class DataIngestionBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<DataIngestionBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _updateInterval = TimeSpan.FromHours(1); // Run every hour

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Data Ingestion Background Service is starting");

        // Wait a bit on startup to let the application fully initialize
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Starting scheduled data ingestion cycle");

                // Create a scope to resolve scoped services
                using (var scope = serviceProvider.CreateScope())
                {
                    var orchestrator = scope.ServiceProvider
                        .GetRequiredService<IDataIngestionOrchestrator>();

                    await orchestrator.RunScheduledUpdatesAsync();
                }

                logger.LogInformation("Scheduled data ingestion cycle complete");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during scheduled data ingestion");
            }

            // Wait for the next cycle
            try
            {
                await Task.Delay(_updateInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
        }

        logger.LogInformation("Data Ingestion Background Service is stopping");
    }
}