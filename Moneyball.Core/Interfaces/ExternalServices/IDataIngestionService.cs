namespace Moneyball.Core.Interfaces.ExternalServices
{
    public interface IDataIngestionService
    {
        Task IngestNBAScheduleAsync(DateTime startDate, DateTime endDate);
        Task IngestNBATeamsAsync();
        Task IngestNBAGameStatisticsAsync(string externalGameId);
        Task IngestOddsAsync(string sport);
        Task UpdateGameResultsAsync(int sportId);
    }
}
