using Moneyball.Domain.NBA;

namespace Moneyball.ML.NBA
{
    public interface INbaPredictionModel
    {
        float Predict(NbaFeatureSet features);
    }
}
