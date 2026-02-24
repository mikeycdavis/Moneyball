using Microsoft.ML;
using Moneyball.Core.DTOs;
using Moneyball.Core.DTOs.ML;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Exceptions;
using Moneyball.Core.Interfaces.ML;

namespace Moneyball.Infrastructure.ML;

/// <summary>
/// ML.NET model executor implementation.
/// Loads .zip models from FilePath and executes predictions locally in C#.
/// Acceptance criteria: Loads .zip model from FilePath; runs prediction; 
/// returns same PredictionResult shape as Python executor.
/// </summary>
public class MLNetModelExecutor : IModelExecutor
{
    private readonly MLContext _mlContext;
    private readonly Dictionary<string, ITransformer> _modelCache;
    private readonly SemaphoreSlim _cacheLock;

    /// <summary>
    /// Initializes a new instance of MLNetModelExecutor.
    /// Creates ML context and initializes model cache.
    /// </summary>
    public MLNetModelExecutor()
    {
        _mlContext = new MLContext(seed: 0);
        _modelCache = new Dictionary<string, ITransformer>();
        _cacheLock = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Gets the model type this executor supports.
    /// </summary>
    public ModelType SupportedModelType => ModelType.MLNet;

    /// <summary>
    /// Determines if this executor can execute the given model.
    /// </summary>
    /// <param name="model">The model to check</param>
    /// <returns>True if model type is ML.NET</returns>
    /// <exception cref="ArgumentNullException">Thrown when model is null</exception>
    public bool CanExecute(Model model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Type == SupportedModelType;
    }

    /// <summary>
    /// Executes ML.NET model prediction.
    /// Acceptance criteria: 
    /// - Loads .zip model from FilePath
    /// - Runs prediction
    /// - Returns same PredictionResult shape as Python executor
    /// </summary>
    /// <param name="model">Model entity with FilePath to .zip file</param>
    /// <param name="features">Dictionary of feature names to values</param>
    /// <returns>PredictionResult with HomeWinProbability, AwayWinProbability, and Confidence</returns>
    /// <exception cref="ArgumentNullException">Thrown when model or features are null</exception>
    /// <exception cref="ModelExecutionException">Thrown when model execution fails</exception>
    public async Task<PredictionResult> ExecuteAsync(
        Model model,
        Dictionary<string, object> features)
    {
        // Validate inputs (matching PythonModelExecutor pattern)
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(features);

        // Validate FilePath is specified
        if (string.IsNullOrWhiteSpace(model.FilePath))
        {
            throw new ModelExecutionException(
                $"Model '{model.Name}' (Version: {model.Version}) has no FilePath specified.");
        }

        // Validate file exists (acceptance criteria: loads .zip model from FilePath)
        if (!File.Exists(model.FilePath))
        {
            throw new ModelExecutionException(
                $"Model file not found at path: {model.FilePath}");
        }

        try
        {
            // Load model from .zip file (acceptance criteria: loads .zip model)
            var transformer = await LoadModelAsync(model);

            // Run prediction (acceptance criteria: runs prediction)
            return await Task.Run(() =>
            {
                try
                {
                    // Create prediction engine
                    var predictionEngine = _mlContext.Model.CreatePredictionEngine<GameFeatures, GamePrediction>(transformer);

                    // Convert feature dictionary to ML.NET input
                    var input = ConvertFeaturesToInput(features);

                    // Execute prediction
                    var prediction = predictionEngine.Predict(input);

                    // ML.NET binary classification outputs:
                    // - PredictedLabel: bool (true/false)
                    // - Score: float (raw model output)
                    // - Probability: float (calibrated probability for positive class)

                    // For binary classification with bool labels:
                    // - If PredictedLabel is true, Probability is P(Label=true)
                    // - If PredictedLabel is false, we need 1 - Probability for P(Label=false)

                    float homeWinProb;
                    if (prediction.PredictedLabel) // Model predicts home team wins
                    {
                        homeWinProb = prediction.Probability;
                    }
                    else // Model predicts away team wins
                    {
                        homeWinProb = 1.0f - prediction.Probability;
                    }

                    // Ensure probability is in valid range [0, 1]
                    homeWinProb = Math.Clamp(homeWinProb, 0.0f, 1.0f);

                    // Return PredictionResult with same shape as Python executor
                    // (acceptance criteria: returns same PredictionResult shape)
                    return new PredictionResult
                    {
                        HomeWinProbability = (decimal)homeWinProb,
                        AwayWinProbability = (decimal)(1.0f - homeWinProb),
                        Confidence = (decimal)Math.Abs(prediction.Probability - 0.5f) * 2.0m, // Convert to 0-1 scale
                        PredictedAt = DateTime.UtcNow
                    };
                }
                catch (Exception ex) when (ex is not ModelExecutionException)
                {
                    throw new ModelExecutionException(
                        $"Failed to execute ML.NET model '{model.Name}' (Version: {model.Version}).", ex);
                }
            });
        }
        catch (ModelExecutionException)
        {
            // Re-throw ModelExecutionException as-is
            throw;
        }
        catch (Exception ex)
        {
            throw new ModelExecutionException(
                $"Unexpected error executing model '{model.Name}' (Version: {model.Version}).", ex);
        }
    }

    /// <summary>
    /// Loads the ML.NET model from .zip file, using cache if available.
    /// Implements thread-safe caching for performance optimization.
    /// </summary>
    /// <param name="model">Model entity with FilePath</param>
    /// <returns>Loaded ITransformer model</returns>
    private async Task<ITransformer> LoadModelAsync(Model model)
    {
        // Create cache key (similar to PythonModelExecutor's model_name pattern)
        var cacheKey = $"{model.Name}_{model.Version}";

        // Check cache first (read lock not needed for TryGetValue)
        if (_modelCache.TryGetValue(cacheKey, out var cachedModel))
        {
            return cachedModel;
        }

        // Load model with exclusive lock
        await _cacheLock.WaitAsync();
        try
        {
            // Double-check cache after acquiring lock
            if (_modelCache.TryGetValue(cacheKey, out var recheck))
            {
                return recheck;
            }

            // Load model from file on background thread
            var loadedModel = await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(model.FilePath))
                    {
                        throw new ModelExecutionException(
                            $"Model '{model.Name}' (Version: {model.Version}) has no FilePath specified.");
                    }

                    // Load .zip model file (acceptance criteria)
                    using var stream = File.OpenRead(model.FilePath);
                    return _mlContext.Model.Load(stream, out _);
                }
                catch (Exception ex)
                {
                    throw new ModelExecutionException(
                        $"Failed to load ML.NET model from path: {model.FilePath}", ex);
                }
            });

            // Cache the loaded model
            _modelCache[cacheKey] = loadedModel;

            return loadedModel;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Converts feature dictionary to ML.NET input object.
    /// Uses reflection to dynamically map feature names to properties.
    /// </summary>
    /// <param name="features">Dictionary of feature names to values</param>
    /// <returns>GameFeatures object with populated properties</returns>
    private static GameFeatures ConvertFeaturesToInput(Dictionary<string, object> features)
    {
        try
        {
            var input = new GameFeatures();

            // Map features to properties using reflection
            var properties = typeof(GameFeatures).GetProperties();
            foreach (var property in properties)
            {
                if (features.TryGetValue(property.Name, out var value))
                {
                    // Convert value to property type
                    var convertedValue = Convert.ChangeType(value, property.PropertyType);
                    property.SetValue(input, convertedValue);
                }
                // Properties not in features dictionary will use default values (0.0f)
            }

            return input;
        }
        catch (Exception ex)
        {
            throw new ModelExecutionException(
                "Failed to convert feature dictionary to ML.NET input format.", ex);
        }
    }

    /// <summary>
    /// Clears the model cache. Useful for testing or when models are updated.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync();
        try
        {
            _modelCache.Clear();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    /// Gets the number of cached models. Useful for monitoring.
    /// </summary>
    public int CachedModelCount => _modelCache.Count;
}