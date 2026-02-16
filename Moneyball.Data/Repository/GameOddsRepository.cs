using Microsoft.EntityFrameworkCore;
using Moneyball.Data.Entities;

namespace Moneyball.Data.Repository;

public interface IGameOddsRepository : IRepository<GameOdds>
{
    Task<GameOdds?> GetLatestOddsAsync(int gameId, string? bookmaker = null);
    Task<IEnumerable<GameOdds>> GetOddsHistoryAsync(int gameId);
    Task<IEnumerable<GameOdds>> GetLatestOddsForGamesAsync(IEnumerable<int> gameIds);
}

public class GameOddsRepository(MoneyballDbContext context) : Repository<GameOdds>(context), IGameOddsRepository
{
    public async Task<GameOdds?> GetLatestOddsAsync(int gameId, string? bookmaker = null)
    {
        var query = _dbSet.Where(o => o.GameId == gameId);

        if (!string.IsNullOrEmpty(bookmaker))
        {
            query = query.Where(o => o.BookmakerName == bookmaker);
        }

        return await query.OrderByDescending(o => o.RecordedAt).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<GameOdds>> GetOddsHistoryAsync(int gameId)
    {
        return await _dbSet
            .Where(o => o.GameId == gameId)
            .OrderByDescending(o => o.RecordedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<GameOdds>> GetLatestOddsForGamesAsync(IEnumerable<int> gameIds)
    {
        var gameIdList = gameIds.ToList();

        return await _dbSet
            .Where(o => gameIdList.Contains(o.GameId))
            .GroupBy(o => o.GameId)
            .Select(g => g.OrderByDescending(o => o.RecordedAt).First())
            .ToListAsync();
    }
}