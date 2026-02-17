namespace Moneyball.Core.Interfaces.ExternalAPIs
{
    public interface IDataIngestionOrchestrator
    {
        Task RunFullIngestionAsync(int sportId);
        Task RunScheduledUpdatesAsync();
    }
}
