using Moneyball.Data.Entities;

namespace Moneyball.Service.ML
{
    public interface IModelService
    {
        Task<Prediction> GeneratePredictionAsync(int modelId, int gameId);
        Task<List<Prediction>> GeneratePredictionsForAllActiveModelsAsync(int gameId);
        Task<Model> RegisterModelAsync(ModelRegistrationDto dto);
        Task<List<Model>> GetActiveModelsAsync(int sportId);
    }
}
