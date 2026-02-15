using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Moneyball.Domain.NBA;

namespace Moneyball.ML.NBA
{
    public class OnnxNbaPredictionModel : INbaPredictionModel
    {
        private readonly InferenceSession _session;

        public OnnxNbaPredictionModel(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        public float Predict(NbaFeatureSet features)
        {
            var inputData = new float[]
            {
                features.EloDiff,
                features.RestDiff,
                features.BackToBackDiff,
                features.InjuryMinutesLostDiff,
                features.HomeAdvantage
            };

            var tensor = new DenseTensor<float>(inputData, new[] { 1, inputData.Length });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input", tensor)
            };

            using var results = _session.Run(inputs);
            return results.First().AsEnumerable<float>().First();
        }
    }
}
