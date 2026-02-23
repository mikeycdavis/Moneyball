namespace Moneyball.Core.DTOs
{
    /// <summary>
    /// Result of a model prediction execution.
    /// Contains win probabilities for both teams and confidence score.
    /// Acceptance criteria: HomeWinProbability, AwayWinProbability, and Confidence.
    /// </summary>
    public class PredictionResult
    {
        /// <summary>
        /// Model name
        /// </summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>
        /// Model version
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Probability that the home team will win (0.0 to 1.0)
        /// </summary>
        public decimal HomeWinProbability { get; set; }

        /// <summary>
        /// Probability that the away team will win (0.0 to 1.0)
        /// </summary>
        public decimal AwayWinProbability { get; set; }

        /// <summary>
        /// Edge for this prediction
        /// </summary>
        public decimal? Edge { get; set; }

        /// <summary>
        /// Confidence score for this prediction (0.0 to 1.0)
        /// Higher values indicate more confident predictions
        /// </summary>
        public decimal? Confidence { get; set; }

        /// <summary>
        /// Additional metadata about the prediction
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }

        /// <summary>
        /// Timestamp when the prediction was generated
        /// </summary>
        public DateTime PredictedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Optional: Draw probability for sports that support ties
        /// </summary>
        public decimal? DrawProbability { get; set; }

        /// <summary>
        /// Validates that probabilities sum to approximately 1.0
        /// </summary>
        public bool IsValid()
        {
            var sum = HomeWinProbability + AwayWinProbability + (DrawProbability ?? 0);
            return sum >= 0.95m && sum <= 1.05m; // Allow small floating point error
        }
    }
}
