using FluentAssertions;
using Microsoft.ML;
using Moneyball.Core.DTOs.ML;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Exceptions;
using Moneyball.Infrastructure.ML;

// Required NuGet Packages:
// - Microsoft.ML (core package for ML.NET)
// - FluentAssertions (for readable assertions)
// - Moq (for mocking - though not needed here, kept for consistency)
// - xUnit (test framework)

namespace Moneyball.Tests.ML;

/// <summary>
/// Unit tests for MLNetModelExecutor class.
/// Tests model loading from .zip files, prediction execution, and error handling.
/// Validates acceptance criteria: loads .zip from FilePath; runs prediction; 
/// returns same PredictionResult shape as Python executor.
/// Uses FluentAssertions for readable assertions.
/// </summary>
public class MLNetModelExecutorTests : IDisposable
{
    private readonly MLNetModelExecutor _executor;
    private readonly string _testModelPath;
    private readonly MLContext _mlContext;

    /// <summary>
    /// Test fixture setup - creates test ML.NET model.
    /// Runs before each test to ensure clean state.
    /// </summary>
    public MLNetModelExecutorTests()
    {
        _executor = new MLNetModelExecutor();
        _mlContext = new MLContext(seed: 0);
        _testModelPath = Path.Combine(Path.GetTempPath(), $"test_model_{Guid.NewGuid()}.zip");

        // Create a simple test model for use in tests
        CreateTestModel();
    }

    /// <summary>
    /// Creates a simple binary classification model for testing.
    /// Uses a simple linear model that works with boolean labels.
    /// Saves model as .zip file (matching acceptance criteria format).
    /// </summary>
    private void CreateTestModel()
    {
        // Define the training data inline with explicit column names
        var data = new[]
        {
            new TrainingData { HomeWinRate = 0.7f, AwayWinRate = 0.3f, HomePointsAvg = 110f, AwayPointsAvg = 95f, Label = true },
            new TrainingData { HomeWinRate = 0.65f, AwayWinRate = 0.35f, HomePointsAvg = 108f, AwayPointsAvg = 97f, Label = true },
            new TrainingData { HomeWinRate = 0.6f, AwayWinRate = 0.4f, HomePointsAvg = 105f, AwayPointsAvg = 100f, Label = true },
            new TrainingData { HomeWinRate = 0.55f, AwayWinRate = 0.45f, HomePointsAvg = 103f, AwayPointsAvg = 102f, Label = true },
            new TrainingData { HomeWinRate = 0.45f, AwayWinRate = 0.55f, HomePointsAvg = 98f, AwayPointsAvg = 107f, Label = false },
            new TrainingData { HomeWinRate = 0.4f, AwayWinRate = 0.6f, HomePointsAvg = 95f, AwayPointsAvg = 110f, Label = false },
            new TrainingData { HomeWinRate = 0.35f, AwayWinRate = 0.65f, HomePointsAvg = 92f, AwayPointsAvg = 113f, Label = false },
            new TrainingData { HomeWinRate = 0.3f, AwayWinRate = 0.7f, HomePointsAvg = 90f, AwayPointsAvg = 115f, Label = false }
        };

        // Load the training data
        var trainingDataView = _mlContext.Data.LoadFromEnumerable(data);

        //// Build the training pipeline
        //// Step 1: Concatenate features into a single Features vector
        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(TrainingData.HomeWinRate),
                nameof(TrainingData.AwayWinRate),
                nameof(TrainingData.HomePointsAvg),
                nameof(TrainingData.AwayPointsAvg))
            // Train directly on the boolean Label column
            .Append(_mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: nameof(TrainingData.Label),
                featureColumnName: "Features",
                maximumNumberOfIterations: 100));

        // Train the model
        var trainedModel = pipeline.Fit(trainingDataView);

        // Save model to .zip file (acceptance criteria format)
        _mlContext.Model.Save(trainedModel, trainingDataView.Schema, _testModelPath);
    }

    /// <summary>
    /// Training data class for model training.
    /// Separate from GameFeatures to avoid confusion with column names.
    /// </summary>
    private class TrainingData
    {
        public float HomeWinRate { get; set; }
        public float AwayWinRate { get; set; }
        public float HomePointsAvg { get; set; }
        public float AwayPointsAvg { get; set; }
        public bool Label { get; set; }
    }

    /// <summary>
    /// Training data class with label for model training.
    /// Extends GameFeatures with Label property.
    /// Note: This is kept for compatibility but TrainingData is used in CreateTestModel.
    /// </summary>
    private class GameFeaturesWithLabel : GameFeatures
    {
        public bool Label { get; set; }
    }

    /// <summary>
    /// Cleanup - deletes test model file and clears cache.
    /// Runs after each test.
    /// </summary>
    public void Dispose()
    {
        if (File.Exists(_testModelPath))
        {
            File.Delete(_testModelPath);
        }
        _executor.ClearCacheAsync().Wait();

        GC.SuppressFinalize(this);
    }

    // ==================== Acceptance Criteria Tests ====================

    /// <summary>
    /// Tests that executor loads .zip model from FilePath.
    /// Verifies acceptance criteria: Loads .zip model from FilePath.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_LoadsZipModelFromFilePath()
    {
        // Arrange
        var model = new Model
        {
            ModelId = 1,
            Name = "NBA_LogisticRegression",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath // Acceptance criteria: loads from FilePath (.zip)
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.65 },
            { nameof(GameFeatures.AwayWinRate), 0.35 },
            { nameof(GameFeatures.HomePointsAvg), 108.0 },
            { nameof(GameFeatures.AwayPointsAvg), 98.0 }
        };

        // Act
        var result = await _executor.ExecuteAsync(model, features);

        // Assert - Model should load from .zip FilePath and execute successfully
        result.Should().NotBeNull(
            "model should load from .zip FilePath and execute");

        File.Exists(_testModelPath).Should().BeTrue(
            "test model .zip file should exist");
    }

    /// <summary>
    /// Tests that executor runs prediction successfully.
    /// Verifies acceptance criteria: runs prediction.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RunsPrediction()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_XGBoost",
            Version = "2",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.70 },
            { nameof(GameFeatures.AwayWinRate), 0.30 },
            { nameof(GameFeatures.HomePointsAvg), 115.0 },
            { nameof(GameFeatures.AwayPointsAvg), 92.0 }
        };

        // Act - Acceptance criteria: runs prediction
        var result = await _executor.ExecuteAsync(model, features);

        // Assert - Prediction should execute and return valid probabilities
        result.Should().NotBeNull("prediction should execute");
        result.HomeWinProbability.Should().BeGreaterThan(0,
            "should predict non-zero probability");
        result.HomeWinProbability.Should().BeLessThanOrEqualTo(1,
            "probability should not exceed 1.0");
    }

    /// <summary>
    /// Tests that result has same shape as Python executor.
    /// Verifies acceptance criteria: returns same PredictionResult shape as Python executor.
    /// Must have: HomeWinProbability, AwayWinProbability, Confidence, PredictedAt.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ReturnsSamePredictionResultShapeAsPythonExecutor()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_Model",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.60 },
            { nameof(GameFeatures.AwayWinRate), 0.40 },
            { nameof(GameFeatures.HomePointsAvg), 105.0 },
            { nameof(GameFeatures.AwayPointsAvg), 100.0 }
        };

        // Act
        var result = await _executor.ExecuteAsync(model, features);

        // Assert - Same shape as Python executor (acceptance criteria)
        result.Should().NotBeNull();

        // Must have HomeWinProbability (matching Python executor)
        result.HomeWinProbability.Should().BeInRange(0, 1,
            "HomeWinProbability should match Python executor shape (0-1 range)");

        // Must have AwayWinProbability (matching Python executor)
        result.AwayWinProbability.Should().BeInRange(0, 1,
            "AwayWinProbability should match Python executor shape (0-1 range)");

        // Must have Confidence (matching Python executor)
        result.Confidence.Should().BeInRange(0, 1,
            "Confidence should match Python executor shape (0-1 range)");

        // Must have PredictedAt timestamp (matching Python executor)
        result.PredictedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5),
            "PredictedAt should be set like Python executor");
    }

    // ==================== Property Tests ====================

    /// <summary>
    /// Tests that SupportedModelType returns MLNet.
    /// Verifies executor identifies itself correctly.
    /// </summary>
    [Fact]
    public void SupportedModelType_ReturnsMLNet()
    {
        // Act
        var modelType = _executor.SupportedModelType;

        // Assert
        modelType.Should().Be(ModelType.MLNet,
            "MLNetModelExecutor should support MLNet model type");
    }

    /// <summary>
    /// Tests that CanExecute returns true for ML.NET models.
    /// Verifies model type validation for compatible models.
    /// </summary>
    [Fact]
    public void CanExecute_MLNetModel_ReturnsTrue()
    {
        // Arrange
        var model = new Model
        {
            Name = "Test_Model",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        // Act
        var canExecute = _executor.CanExecute(model);

        // Assert
        canExecute.Should().BeTrue(
            "executor should be able to execute ML.NET models");
    }

    /// <summary>
    /// Tests that CanExecute returns false for non-ML.NET models.
    /// Verifies executor rejects incompatible model types.
    /// </summary>
    [Theory]
    [InlineData(ModelType.Python, "Python models should be rejected")]
    [InlineData(ModelType.External, "External models should be rejected")]
    public void CanExecute_NonMLNetModel_ReturnsFalse(ModelType modelType, string because)
    {
        // Arrange
        var model = new Model
        {
            Name = "Test_Model",
            Type = modelType,
            FilePath = "/some/path/model.zip"
        };

        // Act
        var canExecute = _executor.CanExecute(model);

        // Assert
        canExecute.Should().BeFalse(because);
    }

    // ==================== Validation Tests ====================

    /// <summary>
    /// Tests that null model throws ArgumentNullException.
    /// Verifies input validation matches PythonModelExecutor pattern.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        Model? model = null;
        var features = new Dictionary<string, object>();

        // Act
        var act = async () => await _executor.ExecuteAsync(model!, features);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("model",
                "null model should throw ArgumentNullException like PythonModelExecutor");
    }

    /// <summary>
    /// Tests that null features throws ArgumentNullException.
    /// Verifies input validation matches PythonModelExecutor pattern.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NullFeatures_ThrowsArgumentNullException()
    {
        // Arrange
        var model = new Model
        {
            Name = "Test_Model",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };
        Dictionary<string, object>? features = null;

        // Act
        var act = async () => await _executor.ExecuteAsync(model, features!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("features",
                "null features should throw ArgumentNullException like PythonModelExecutor");
    }

    /// <summary>
    /// Tests that CanExecute with null model throws ArgumentNullException.
    /// Verifies validation method safety.
    /// </summary>
    [Fact]
    public void CanExecute_NullModel_ThrowsArgumentNullException()
    {
        // Arrange
        Model? model = null;

        // Act
        var act = () => _executor.CanExecute(model!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("model");
    }

    /// <summary>
    /// Tests that empty or null FilePath throws ModelExecutionException.
    /// Verifies FilePath validation with meaningful error messages.
    /// </summary>
    [Theory]
    [InlineData(null, "null FilePath")]
    [InlineData("", "empty FilePath")]
    [InlineData("   ", "whitespace FilePath")]
    public async Task ExecuteAsync_InvalidFilePath_ThrowsModelExecutionException(
        string? filePath,
        string because)
    {
        // Arrange
        var model = new Model
        {
            Name = "Test_Model",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = filePath!
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.6 }
        };

        // Act
        var act = async () => await _executor.ExecuteAsync(model, features);

        // Assert
        await act.Should().ThrowAsync<ModelExecutionException>()
            .WithMessage("*no FilePath specified*", because);
    }

    /// <summary>
    /// Tests that non-existent file throws ModelExecutionException.
    /// Verifies file existence validation with meaningful error.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_FileNotFound_ThrowsModelExecutionException()
    {
        // Arrange
        var model = new Model
        {
            Name = "NonExistent_Model",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = "/non/existent/path/model.zip"
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.6 }
        };

        // Act
        var act = async () => await _executor.ExecuteAsync(model, features);

        // Assert
        await act.Should().ThrowAsync<ModelExecutionException>()
            .WithMessage("*file not found*");
    }

    // ==================== Probability Calculation Tests ====================

    /// <summary>
    /// Tests that home and away probabilities sum to approximately 1.0.
    /// Verifies probability distribution validity (matching Python executor behavior).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ProbabilitiesSumToOne()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_Model",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.55 },
            { nameof(GameFeatures.AwayWinRate), 0.45 },
            { nameof(GameFeatures.HomePointsAvg), 106.0 },
            { nameof(GameFeatures.AwayPointsAvg), 104.0 }
        };

        // Act
        var result = await _executor.ExecuteAsync(model, features);

        // Assert - Probabilities should sum to approximately 1.0 (matching Python behavior)
        var sum = result.HomeWinProbability + result.AwayWinProbability;
        sum.Should().BeApproximately(1.0m, 0.05m,
            "home and away probabilities should sum to 1.0 like Python executor");
    }

    /// <summary>
    /// Tests that away probability is calculated as 1 - home probability.
    /// Verifies probability complement calculation (matching Python executor pattern).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AwayProbabilityIsComplement()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_Model",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.70 },
            { nameof(GameFeatures.AwayWinRate), 0.30 },
            { nameof(GameFeatures.HomePointsAvg), 112.0 },
            { nameof(GameFeatures.AwayPointsAvg), 95.0 }
        };

        // Act
        var result = await _executor.ExecuteAsync(model, features);

        // Assert - Away probability should be 1 - Home probability
        var expectedAway = 1.0m - result.HomeWinProbability;
        result.AwayWinProbability.Should().BeApproximately(expectedAway, 0.01m,
            "away probability should be complement of home probability");
    }

    // ==================== Caching Tests ====================

    /// <summary>
    /// Tests that models are cached after first load for performance.
    /// Verifies caching optimization (similar to connection pooling pattern).
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_SecondCall_UsesCache()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_Cached",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.6 },
            { nameof(GameFeatures.AwayWinRate), 0.4 },
            { nameof(GameFeatures.HomePointsAvg), 105.0 },
            { nameof(GameFeatures.AwayPointsAvg), 100.0 }
        };

        // Act - First call loads model
        var result1 = await _executor.ExecuteAsync(model, features);

        // Verify model is cached
        _executor.CachedModelCount.Should().Be(1,
            "model should be cached after first execution");

        // Second call should use cache (faster)
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result2 = await _executor.ExecuteAsync(model, features);
        stopwatch.Stop();

        // Assert - Both calls should succeed
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();

        // Second call should be very fast (cached)
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100,
            "cached model execution should be fast");
    }

    /// <summary>
    /// Tests that ClearCacheAsync removes cached models.
    /// Verifies cache management functionality.
    /// </summary>
    [Fact]
    public async Task ClearCacheAsync_RemovesCachedModels()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_ClearCache",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.6 },
            { nameof(GameFeatures.AwayWinRate), 0.4 },
            { nameof(GameFeatures.HomePointsAvg), 105.0 },
            { nameof(GameFeatures.AwayPointsAvg), 100.0 }
        };

        // Load model into cache
        await _executor.ExecuteAsync(model, features);
        _executor.CachedModelCount.Should().Be(1, "model should be cached");

        // Act - Clear cache
        await _executor.ClearCacheAsync();

        // Assert - Cache should be empty
        _executor.CachedModelCount.Should().Be(0,
            "cache should be empty after ClearCacheAsync");

        // Model should still execute after cache clear (reload from file)
        var act = async () => await _executor.ExecuteAsync(model, features);
        await act.Should().NotThrowAsync(
            "model should reload from file after cache clear");
    }

    // ==================== Feature Conversion Tests ====================

    /// <summary>
    /// Tests that feature dictionary converts to ML.NET input correctly.
    /// Verifies feature mapping handles various data types.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ConvertsVariousFeatureTypes_Correctly()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_Features",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        // Features with various numeric types (int, double, float, decimal)
        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.75 },      // double
            { nameof(GameFeatures.AwayWinRate), 0.25f },     // float
            { nameof(GameFeatures.HomePointsAvg), 118 },     // int
            { nameof(GameFeatures.AwayPointsAvg), 88.5m },   // decimal
            { nameof(GameFeatures.RestDaysHome), 2.0 },
            { nameof(GameFeatures.RestDaysAway), 0.0 }
        };

        // Act
        var act = async () => await _executor.ExecuteAsync(model, features);

        // Assert - Should convert all numeric types to float without errors
        await act.Should().NotThrowAsync(
            "feature dictionary with various numeric types should convert correctly");
    }

    /// <summary>
    /// Tests that missing features use default values (0.0f).
    /// Verifies graceful handling of partial feature sets.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MissingFeatures_UsesDefaults()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_Partial",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        // Minimal features (others will use default 0.0f values)
        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.6 },
            { nameof(GameFeatures.AwayWinRate), 0.4 }
            // HomePointsAvg, AwayPointsAvg, etc. will be 0.0f (default)
        };

        // Act
        var act = async () => await _executor.ExecuteAsync(model, features);

        // Assert - Should handle missing features by using defaults
        await act.Should().NotThrowAsync(
            "missing features should use default values (0.0f)");
    }

    // ==================== Concurrent Execution Tests ====================

    /// <summary>
    /// Tests that executor handles concurrent requests safely.
    /// Verifies thread-safety of model loading, caching, and prediction.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ConcurrentCalls_ThreadSafe()
    {
        // Arrange
        var model = new Model
        {
            Name = "NBA_Concurrent",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.6 },
            { nameof(GameFeatures.AwayWinRate), 0.4 },
            { nameof(GameFeatures.HomePointsAvg), 105.0 },
            { nameof(GameFeatures.AwayPointsAvg), 100.0 }
        };

        // Act - Execute 10 predictions concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _executor.ExecuteAsync(model, features))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All concurrent predictions should succeed
        results.Should().HaveCount(10, "all 10 concurrent tasks should complete");
        results.Should().AllSatisfy(r =>
            r.Should().NotBeNull(),
            "all concurrent predictions should return valid results");

        // Model should only be loaded once (cached)
        _executor.CachedModelCount.Should().Be(1,
            "model should be loaded once and cached for concurrent requests");
    }

    /// <summary>
    /// Tests that different model versions don't interfere with each other.
    /// Verifies cache keys properly distinguish model versions.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_DifferentVersions_CachedSeparately()
    {
        // Arrange
        var modelV1 = new Model
        {
            Name = "NBA_Model",
            Version = "1",
            Type = ModelType.MLNet,
            FilePath = _testModelPath
        };

        var modelV2 = new Model
        {
            Name = "NBA_Model",
            Version = "2",
            Type = ModelType.MLNet,
            FilePath = _testModelPath // Same file for testing, but different version
        };

        var features = new Dictionary<string, object>
        {
            { nameof(GameFeatures.HomeWinRate), 0.6 },
            { nameof(GameFeatures.AwayWinRate), 0.4 }
        };

        // Act - Execute both versions
        await _executor.ExecuteAsync(modelV1, features);
        await _executor.ExecuteAsync(modelV2, features);

        // Assert - Both versions should be cached separately
        _executor.CachedModelCount.Should().Be(2,
            "different model versions should be cached separately with keys: Name_Version");
    }
}