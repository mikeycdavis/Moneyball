namespace Moneyball.Core.Interfaces.ExternalServices
{
    public interface IDataIngestionOrchestrator
    {
        Task RunFullIngestionAsync(int sportId);
        Task RunScheduledUpdatesAsync();
    }
}
