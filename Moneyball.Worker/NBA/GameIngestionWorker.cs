using Microsoft.Extensions.Hosting;

namespace Moneyball.Worker.NBA
{
    public class GameIngestionWorker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PullGames();
                await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
            }
        }

        private Task PullGames()
        {
            // TODO: Call NBA API
            return Task.CompletedTask;
        }
    }
}
