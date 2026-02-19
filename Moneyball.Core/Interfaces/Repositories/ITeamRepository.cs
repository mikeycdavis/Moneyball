using Moneyball.Core.Entities;

namespace Moneyball.Core.Interfaces.Repositories
{
    public interface ITeamRepository : IRepository<Team>
    {
        Task<Team?> GetByExternalIdAsync(string externalId, int sportId);
        Task<IEnumerable<Team>> GetBySportAsync(int sportId);
        Task<Team?> GetTeamWithStatsAsync(int teamId);
    }
}
