using Moneyball.Core.Entities;

namespace Moneyball.Core.Interfaces
{
    public interface IPredictionRepository : IRepository<Prediction>
    {
        Task<IEnumerable<Prediction>> GetPredictionsByGameAsync(int gameId);
        Task<IEnumerable<Prediction>> GetPredictionsByModelAsync(int modelId, DateTime? since = null);
        Task<Prediction?> GetLatestPredictionAsync(int gameId, int modelId);
        Task<IEnumerable<Prediction>> GetPredictionsWithHighEdgeAsync(decimal minEdge, int? sportId = null);
    }
}
