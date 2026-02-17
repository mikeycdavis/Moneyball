using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Interfaces;

namespace Moneyball.Infrastructure.Repositories;

public class ModelRepository(MoneyballDbContext context) : Repository<Model>(context), IModelRepository
{
    public async Task<IEnumerable<Model>> GetActiveModelsAsync(int sportId)
    {
        return await _dbSet
            .Include(m => m.Sport)
            .Where(m => m.SportId == sportId && m.IsActive)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Model?> GetByNameAndVersionAsync(string name, string version)
    {
        return await _dbSet
            .Include(m => m.Sport)
            .FirstOrDefaultAsync(m => m.Name == name && m.Version == version);
    }

    public async Task<IEnumerable<Model>> GetModelsByTypeAsync(ModelType modelType)
    {
        return await _dbSet
            .Include(m => m.Sport)
            .Where(m => m.ModelType == modelType && m.IsActive)
            .ToListAsync();
    }
}