using Microsoft.Extensions.Logging;
using Moneyball.Core.Enums;
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
            switch (sportId)
            {
                // NBA
                case (int)SportType.NBA:
                    // Step 1: Ingest teams (if not already done)
                    logger.LogInformation("Ingesting NBA teams...");
                    await dataIngestionService.IngestNBATeamsAsync();

                    // Step 2: Ingest schedule for next 14 days
                    logger.LogInformation("Ingesting NBA schedule...");
                    await dataIngestionService.IngestNBAScheduleAsync(
                        DateTime.UtcNow.Date.AddDays(-1),
                        DateTime.UtcNow.Date.AddDays(14));

                    // Step 3: Ingest odds for the last 48 hours
                    logger.LogInformation("Ingesting NBA odds...");
                    await dataIngestionService.IngestNBAOddsAsync(
                        DateTime.UtcNow.AddHours(-48),
                        DateTime.UtcNow.AddHours(1));
                    break;
                // NFL
                case (int)SportType.NFL:
                    // Similar implementation for NFL
                    logger.LogInformation("NFL ingestion not yet implemented");
                    break;
                // NHL
                case (int)SportType.NHL:
                    // Similar implementation for NHL
                    logger.LogInformation("NHL ingestion not yet implemented");
                    break;
                // MLB
                case (int)SportType.MLB:
                    // Similar implementation for MLB
                    logger.LogInformation("MLB ingestion not yet implemented");
                    break;
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
                        // Step 1: Ingest schedule for next 14 days
                        logger.LogInformation("Updating NBA schedule...");
                        await dataIngestionService.IngestNBAScheduleAsync(
                            DateTime.UtcNow.AddDays(-1),
                            DateTime.UtcNow.Date.AddDays(14));
                        
                        // Step 2: Ingest odds for the last 48 hours
                        logger.LogInformation("Updating NBA odds...");
                        await dataIngestionService.IngestNBAOddsAsync(
                            DateTime.UtcNow.AddHours(-48),
                            DateTime.UtcNow.AddHours(1));
                        
                        // Step 3: Update game results for the last 48 hours
                        logger.LogInformation("Updating NBA game results...");
                        await dataIngestionService.UpdateNBAGameResultsAsync(
                            DateTime.UtcNow.AddHours(-48),
                            DateTime.UtcNow.AddHours(1));
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