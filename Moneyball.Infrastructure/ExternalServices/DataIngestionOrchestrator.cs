using Microsoft.Extensions.Logging;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Core.Interfaces.ExternalServices;

namespace Moneyball.Infrastructure.ExternalServices;

public class DataIngestionOrchestrator(
    IDataIngestionService dataIngestionService,
    ILogger<DataIngestionOrchestrator> logger) : IDataIngestionOrchestrator
{
    public async Task RunFullIngestionAsync(int sportId)
    {
        logger.LogInformation("Starting full data ingestion for sport {SportId}", sportId);

        try
        {
            // Step 1: Ingest teams (if not already done)
            if (sportId == 1) // NBA
            {
                logger.LogInformation("Ingesting NBA teams...");
                await dataIngestionService.IngestNBATeamsAsync();

                // Step 2: Ingest schedule for next 14 days
                logger.LogInformation("Ingesting NBA schedule...");
                await dataIngestionService.IngestNBAScheduleAsync(
                    DateTime.UtcNow.Date,
                    DateTime.UtcNow.Date.AddDays(14));

                // Step 3: Ingest odds
                logger.LogInformation("Ingesting NBA odds...");
                await dataIngestionService.IngestOddsAsync("basketball_nba");
            }
            else if (sportId == 2) // NFL
            {
                // Similar implementation for NFL
                logger.LogInformation("NFL ingestion not yet implemented");
            }

            logger.LogInformation("Full data ingestion complete for sport {SportId}", sportId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during full ingestion for sport {SportId}", sportId);
            throw;
        }
    }

    public async Task RunScheduledUpdatesAsync()
    {
        logger.LogInformation("Starting scheduled updates");

        try
        {
            var tasks = new List<Task>
            {
                // Update NBA data
                Task.Run(async () =>
                {
                    try
                    {
                        logger.LogInformation("Updating NBA schedule...");
                        await dataIngestionService.IngestNBAScheduleAsync(
                            DateTime.UtcNow.Date.AddDays(-1),
                            DateTime.UtcNow.Date.AddDays(7));

                        logger.LogInformation("Updating NBA odds...");
                        await dataIngestionService.IngestOddsAsync("basketball_nba");

                        logger.LogInformation("Updating NBA game results...");
                        await dataIngestionService.UpdateGameResultsAsync(1);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error during NBA scheduled update");
                    }
                })
            };

            // Add NFL updates when implemented
            // tasks.Add(UpdateNFLData());

            await Task.WhenAll(tasks);

            logger.LogInformation("Scheduled updates complete");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during scheduled updates");
            throw;
        }
    }
}