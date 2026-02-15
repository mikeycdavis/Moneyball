using Moneyball.Domain.NBA;
using Moneyball.ML.NBA;
using Moneyball.Shared;

namespace Moneyball.Service.NBA
{
    public class PredictionService
    {
        private readonly INbaPredictionModel _model;

        public PredictionService(INbaPredictionModel model)
        {
            _model = model;
        }

        public NbaPrediction GeneratePrediction(NbaFeatureSet features, float impliedProbability)
        {
            var homeProb = _model.Predict(features);
            var edge = homeProb - impliedProbability;

            return new NbaPrediction
            {
                GameId = features.GameId,
                HomeOrAway = nameof(HomeOrAway.Home),
                WinProbability = homeProb,
                Edge = edge,
                Confidence = CalculateConfidence(edge),
                CreatedAt = DateTime.UtcNow
            };
        }

        private string CalculateConfidence(float edge)
        {
            if (edge >= 0.08f) return "High";
            if (edge >= 0.05f) return "Medium";
            return "Low";
        }
    }
}
