using Moneyball.Core.DTOs.ExternalAPIs.SportsRadar;

namespace Moneyball.Core.Interfaces.ExternalAPIs
{
    public interface ISportsDataService
    {
        Task<IEnumerable<NBAGame>> GetNBAScheduleAsync(DateTime startDate, DateTime endDate);
        Task<NBAGameStatistics?> GetNBAGameStatisticsAsync(string gameId);
        Task<IEnumerable<NBATeamInfo>> GetNBATeamsAsync();
        Task<NBAOddsResponse?> GetNBAOddsAsync(string gameId);
    }
}
