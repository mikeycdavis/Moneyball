using Moneyball.Core.Entities;

namespace Moneyball.Core.Interfaces
{
    public interface IGameRepository : IRepository<Game>
    {
        Task<IEnumerable<Game>> GetUpcomingGamesAsync(int? sportId = null, int daysAhead = 7);
        Task<IEnumerable<Game>> GetGamesByDateRangeAsync(DateTime startDate, DateTime endDate, int? sportId = null);
        Task<Game?> GetGameWithDetailsAsync(int gameId);
        Task<Game?> GetGameByExternalIdAsync(string externalId, int sportId);
        Task<IEnumerable<Game>> GetGamesNeedingOddsUpdateAsync(int hoursOld = 1);
    }
}
