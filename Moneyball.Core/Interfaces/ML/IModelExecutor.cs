using Moneyball.Core.DTOs;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Exceptions;

namespace Moneyball.Core.Interfaces.ML
{
    /// <summary>
    /// Common contract for executing ML models.
    /// Provides interchangeable interface for Python, ML.NET, and other model types.
    /// Acceptance criteria: ExecuteAsync returns PredictionResult with probabilities and confidence.
    /// </summary>
    public interface IModelExecutor
    {
        /// <summary>
        /// Executes the ML model with provided features and returns predictions.
        /// </summary>
        /// <param name="model">The model to execute (contains metadata and file path)</param>
        /// <param name="features">Dictionary of feature names to values for prediction</param>
        /// <returns>Prediction result with win probabilities and confidence score</returns>
        /// <exception cref="ArgumentNullException">Thrown when model or features are null</exception>
        /// <exception cref="ModelExecutionException">Thrown when model execution fails</exception>
        Task<PredictionResult> ExecuteAsync(Model model, Dictionary<string, object> features);

        /// <summary>
        /// Gets the model type this executor supports (e.g., "Python", "ML.NET", "ONNX")
        /// </summary>
        ModelType SupportedModelType { get; }

        /// <summary>
        /// Validates that the model can be executed by this executor.
        /// </summary>
        /// <param name="model">The model to validate</param>
        /// <returns>True if the model is compatible with this executor</returns>
        bool CanExecute(Model model);
    }
}

namespace SportsBetting.Core.Entities
{
    /// <summary>
    /// Represents a trained ML model stored in the system.
    /// Contains metadata and file location for model execution.
    /// </summary>
    public class Model
    {
        public int ModelId { get; set; }

        /// <summary>
        /// Unique identifier for the model (e.g., "NBA_LogisticRegression_v2")
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Type of model (e.g., "Python", "ML.NET", "ONNX", "TensorFlow")
        /// </summary>
        public string ModelType { get; set; } = string.Empty;

        /// <summary>
        /// Sport this model is trained for (e.g., "NBA", "NFL")
        /// </summary>
        public string Sport { get; set; } = string.Empty;

        /// <summary>
        /// Version number for model tracking
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// File path to the trained model file
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether this model is currently active for predictions
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Model performance metrics (JSON serialized)
        /// </summary>
        public string? PerformanceMetrics { get; set; }

        /// <summary>
        /// Feature names expected by this model (JSON serialized array)
        /// </summary>
        public string? ExpectedFeatures { get; set; }

        /// <summary>
        /// When the model was trained
        /// </summary>
        public DateTime TrainedAt { get; set; }

        /// <summary>
        /// Who trained the model (user or system)
        /// </summary>
        public string TrainedBy { get; set; } = "System";

        /// <summary>
        /// Audit fields
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

namespace SportsBetting.Core.DTOs.ML
{
    /// <summary>
    /// Request DTO for model prediction
    /// </summary>
    public class PredictionRequest
    {
        /// <summary>
        /// ID of the model to use for prediction
        /// </summary>
        public int ModelId { get; set; }

        /// <summary>
        /// Game ID if predicting for a specific game
        /// </summary>
        public int? GameId { get; set; }

        /// <summary>
        /// Home team ID
        /// </summary>
        public int HomeTeamId { get; set; }

        /// <summary>
        /// Away team ID
        /// </summary>
        public int AwayTeamId { get; set; }

        /// <summary>
        /// Feature values for prediction
        /// </summary>
        public Dictionary<string, object> Features { get; set; } = new();

        /// <summary>
        /// Optional: Use cached prediction if available
        /// </summary>
        public bool UseCache { get; set; } = true;
    }

    /// <summary>
    /// Response DTO for model prediction
    /// </summary>
    public class PredictionResponse
    {
        /// <summary>
        /// ID of the model used
        /// </summary>
        public int ModelId { get; set; }

        /// <summary>
        /// Name of the model used
        /// </summary>
        public string ModelName { get; set; } = string.Empty;

        /// <summary>
        /// Prediction result
        /// </summary>
        public PredictionResult Result { get; set; } = new();

        /// <summary>
        /// Whether this prediction was served from cache
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        /// Execution time in milliseconds
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// Any warnings or messages about the prediction
        /// </summary>
        public List<string>? Warnings { get; set; }
    }

    /// <summary>
    /// DTO for registering a new model
    /// </summary>
    public class RegisterModelRequest
    {
        public string ModelName { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public string Sport { get; set; } = string.Empty;
        public int Version { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public string? PerformanceMetrics { get; set; }
        public string? ExpectedFeatures { get; set; }
        public string TrainedBy { get; set; } = "System";
    }

    /// <summary>
    /// DTO for model performance metrics
    /// </summary>
    public class ModelMetrics
    {
        public decimal Accuracy { get; set; }
        public decimal Precision { get; set; }
        public decimal Recall { get; set; }
        public decimal F1Score { get; set; }
        public decimal AUC { get; set; }
        public decimal LogLoss { get; set; }
        public int TrainingSamples { get; set; }
        public int ValidationSamples { get; set; }
        public Dictionary<string, decimal>? FeatureImportance { get; set; }
    }
}