using Moneyball.Core.Entities;

namespace Moneyball.Core.Interfaces
{
    public interface IGameOddsRepository : IRepository<GameOdds>
    {
        Task<GameOdds?> GetLatestOddsAsync(int gameId, string? bookmaker = null);
        Task<IEnumerable<GameOdds>> GetOddsHistoryAsync(int gameId);
        Task<IEnumerable<GameOdds>> GetLatestOddsForGamesAsync(IEnumerable<int> gameIds);
    }
}
