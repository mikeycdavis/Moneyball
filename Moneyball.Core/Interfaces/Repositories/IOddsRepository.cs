using Moneyball.Core.Entities;

namespace Moneyball.Core.Interfaces.Repositories
{
    public interface IOddsRepository : IRepository<Odds>
    {
        Task<Odds?> GetLatestOddsAsync(int gameId, string? bookmaker = null);
        Task<IEnumerable<Odds>> GetOddsHistoryAsync(int gameId);
        Task<IEnumerable<Odds>> GetLatestOddsForGamesAsync(IEnumerable<int> gameIds);
    }
}
