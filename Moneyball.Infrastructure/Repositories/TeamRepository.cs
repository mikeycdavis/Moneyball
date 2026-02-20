using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Interfaces.Repositories;

namespace Moneyball.Infrastructure.Repositories;

public class TeamRepository(MoneyballDbContext context) : Repository<Team>(context), ITeamRepository
{
    public async Task<Team?> GetByExternalIdAsync(string externalId, int sportId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(t => t.ExternalId == externalId && t.SportId == sportId);
    }

    public async Task<IEnumerable<Team>> GetBySportAsync(int sportId)
    {
        return await _dbSet
            .Where(t => t.SportId == sportId)
            .OrderBy(t => t.Name)
            .ToListAsync();
    }

    public async Task<Team?> GetTeamWithStatsAsync(int teamId)
    {
        return await _dbSet
            .Include(t => t.Sport)
            .FirstOrDefaultAsync(t => t.TeamId == teamId);
    }
}