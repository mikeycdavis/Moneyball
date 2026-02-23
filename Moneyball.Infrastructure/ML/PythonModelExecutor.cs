using Microsoft.Extensions.Configuration;
using Moneyball.Core.DTOs;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Exceptions;
using Moneyball.Core.Interfaces.ML;
using System.Net.Http.Json;
using System.Text.Json;

namespace Moneyball.Infrastructure.ML;

public class PythonModelExecutor(HttpClient httpClient, IConfiguration config) : IModelExecutor
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly string _pythonServiceUrl = config["PythonMLService:Url"]
                                                ?? throw new InvalidOperationException("PythonMLService:Url configuration is missing.");

    public ModelType SupportedModelType => ModelType.Python;

    public bool CanExecute(Model model) => model.ModelType == SupportedModelType;

    public async Task<PredictionResult> ExecuteAsync(
        Model model,
        Dictionary<string, object> features)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(features);

        var modelName = $"{model.Name}_{model.Version}";
        var request = new
        {
            model_name = modelName,
            features
        };

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsJsonAsync($"{_pythonServiceUrl}/predict", request);
        }
        catch (HttpRequestException ex)
        {
            throw new ModelExecutionException(
                $"Failed to reach Python ML service for model '{modelName}'. " +
                $"Endpoint: {_pythonServiceUrl}/predict", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new ModelExecutionException(
                $"Request timed out while executing model '{modelName}'.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new ModelExecutionException(
                $"Python ML service returned {(int)response.StatusCode} ({response.ReasonPhrase}) " +
                $"for model '{modelName}'. Response body: {body}");
        }

        try
        {
            var result = await response.Content.ReadFromJsonAsync<PredictionResult>();
            return result ?? throw new ModelExecutionException(
                $"Python ML service returned an empty response for model '{modelName}'.");
        }
        catch (JsonException ex)
        {
            throw new ModelExecutionException(
                $"Failed to deserialize PredictionResult from Python ML service " +
                $"for model '{modelName}'.", ex);
        }
    }
}