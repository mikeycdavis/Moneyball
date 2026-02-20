using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Interfaces.Repositories;

namespace Moneyball.Infrastructure.Repositories;

public class GameRepository(MoneyballDbContext context) : Repository<Game>(context), IGameRepository
{
    public async Task<IEnumerable<Game>> GetUpcomingGamesAsync(int? sportId = null, int daysAhead = 7)
    {
        var query = _dbSet
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.Sport)
            .Where(g => g.GameDate >= DateTime.UtcNow &&
                       g.GameDate <= DateTime.UtcNow.AddDays(daysAhead) &&
                       g.Status == GameStatus.Scheduled);

        if (sportId.HasValue)
        {
            query = query.Where(g => g.SportId == sportId.Value);
        }

        return await query.OrderBy(g => g.GameDate).ToListAsync();
    }

    public async Task<IEnumerable<Game>> GetGamesByDateRangeAsync(DateTime startDate, DateTime endDate, int? sportId = null)
    {
        var query = _dbSet
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.Sport)
            .Where(g => g.GameDate >= startDate && g.GameDate <= endDate);

        if (sportId.HasValue)
        {
            query = query.Where(g => g.SportId == sportId.Value);
        }

        return await query.OrderBy(g => g.GameDate).ToListAsync();
    }

    public async Task<Game?> GetGameWithDetailsAsync(int gameId)
    {
        return await _dbSet
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .Include(g => g.Sport)
            .Include(g => g.Odds)
            .Include(g => g.TeamStatistics)
            .Include(g => g.Predictions)
                .ThenInclude(p => p.Model)
            .FirstOrDefaultAsync(g => g.GameId == gameId);
    }

    public async Task<Game?> GetGameByExternalIdAsync(string externalId, int sportId)
    {
        return await _dbSet
            .Include(g => g.HomeTeam)
            .Include(g => g.AwayTeam)
            .FirstOrDefaultAsync(g => g.ExternalGameId == externalId && g.SportId == sportId);
    }

    public async Task<IEnumerable<Game>> GetGamesNeedingOddsUpdateAsync(int hoursOld = 1)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-hoursOld);

        return await _dbSet
            .Include(g => g.Odds)
            .Where(g => g.Status == GameStatus.Scheduled &&
                       g.GameDate > DateTime.UtcNow &&
                       (!g.Odds.Any() || g.Odds.Max(o => o.RecordedAt) < cutoffTime))
            .ToListAsync();
    }
}
