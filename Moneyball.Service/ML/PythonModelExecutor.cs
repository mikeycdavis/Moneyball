using Microsoft.Extensions.Configuration;
using Moneyball.Data.Entities;
using System.Net.Http.Json;

namespace Moneyball.Service.ML
{
    public class PythonModelExecutor : IModelExecutor
    {
        private readonly HttpClient _httpClient;
        private readonly string _pythonServiceUrl;

        public PythonModelExecutor(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _pythonServiceUrl = config["PythonMLService:Url"];
        }

        public async Task<PredictionResult> ExecuteAsync(
            Model model,
            Dictionary<string, object> features)
        {
            var request = new
            {
                model_name = $"{model.Name}_{model.Version}",
                features = features
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_pythonServiceUrl}/predict", request);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PredictionResult>();
        }
    }
}
