namespace Moneyball.Core.Interfaces.ExternalServices
{
    public interface IDataIngestionService
    {
        Task IngestNBAScheduleAsync(DateTime startDate, DateTime endDate);
        Task IngestNBATeamsAsync();
        Task IngestNBAGameStatisticsAsync(DateTime startDate, DateTime endDate);
        Task IngestNBAOddsAsync(DateTime startDate, DateTime endDate);
        Task IngestOddsAsync(string sport);
        Task UpdateNBAGameResultsAsync(DateTime startDate, DateTime endDate);
    }
}
