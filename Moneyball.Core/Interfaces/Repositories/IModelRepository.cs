using Moneyball.Core.Entities;
using Moneyball.Core.Enums;

namespace Moneyball.Core.Interfaces.Repositories
{
    public interface IModelRepository : IRepository<Model>
    {
        Task<IEnumerable<Model>> GetActiveModelsAsync(int sportId);
        Task<Model?> GetByNameAndVersionAsync(string name, string version);
        Task<IEnumerable<Model>> GetModelsByTypeAsync(ModelType modelType);
    }
}
