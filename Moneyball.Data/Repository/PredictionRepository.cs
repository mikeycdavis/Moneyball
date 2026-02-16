using Microsoft.EntityFrameworkCore;
using Moneyball.Data.Entities;
using Moneyball.Data.Enums;

namespace Moneyball.Data.Repository;

public interface IPredictionRepository : IRepository<Prediction>
{
    Task<IEnumerable<Prediction>> GetPredictionsByGameAsync(int gameId);
    Task<IEnumerable<Prediction>> GetPredictionsByModelAsync(int modelId, DateTime? since = null);
    Task<Prediction?> GetLatestPredictionAsync(int gameId, int modelId);
    Task<IEnumerable<Prediction>> GetPredictionsWithHighEdgeAsync(decimal minEdge, int? sportId = null);
}

public class PredictionRepository(MoneyballDbContext context) : Repository<Prediction>(context), IPredictionRepository
{
    public async Task<IEnumerable<Prediction>> GetPredictionsByGameAsync(int gameId)
    {
        return await _dbSet
            .Include(p => p.Model)
            .Include(p => p.Game)
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Prediction>> GetPredictionsByModelAsync(int modelId, DateTime? since = null)
    {
        var query = _dbSet
            .Include(p => p.Game)
                .ThenInclude(g => g.HomeTeam)
            .Include(p => p.Game)
                .ThenInclude(g => g.AwayTeam)
            .Where(p => p.ModelId == modelId);

        if (since.HasValue)
        {
            query = query.Where(p => p.CreatedAt >= since.Value);
        }

        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<Prediction?> GetLatestPredictionAsync(int gameId, int modelId)
    {
        return await _dbSet
            .Include(p => p.Model)
            .Include(p => p.Game)
            .Where(p => p.GameId == gameId && p.ModelId == modelId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Prediction>> GetPredictionsWithHighEdgeAsync(decimal minEdge, int? sportId = null)
    {
        var query = _dbSet
            .Include(p => p.Game)
                .ThenInclude(g => g.HomeTeam)
            .Include(p => p.Game)
                .ThenInclude(g => g.AwayTeam)
            .Include(p => p.Model)
            .Where(p => p.Edge >= minEdge &&
                       p.Game.Status == GameStatus.Scheduled &&
                       p.Game.GameDate > DateTime.UtcNow);

        if (sportId.HasValue)
        {
            query = query.Where(p => p.Game.SportId == sportId.Value);
        }

        return await query.OrderByDescending(p => p.Edge).ToListAsync();
    }
}