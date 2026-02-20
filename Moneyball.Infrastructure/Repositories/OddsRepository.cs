using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Interfaces.Repositories;

namespace Moneyball.Infrastructure.Repositories;

public class OddsRepository(MoneyballDbContext context) : Repository<Odds>(context), IOddsRepository
{
    public async Task<Odds?> GetLatestOddsAsync(int gameId, string? bookmaker = null)
    {
        var query = _dbSet.Where(o => o.GameId == gameId);

        if (!string.IsNullOrEmpty(bookmaker))
        {
            query = query.Where(o => o.BookmakerName == bookmaker);
        }

        return await query.OrderByDescending(o => o.RecordedAt).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Odds>> GetOddsHistoryAsync(int gameId)
    {
        return await _dbSet
            .Where(o => o.GameId == gameId)
            .OrderByDescending(o => o.RecordedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Odds>> GetLatestOddsForGamesAsync(IEnumerable<int> gameIds)
    {
        var gameIdList = gameIds.ToList();

        return await _dbSet
            .Where(o => gameIdList.Contains(o.GameId))
            .GroupBy(o => o.GameId)
            .Select(g => g.OrderByDescending(o => o.RecordedAt).First())
            .ToListAsync();
    }
}