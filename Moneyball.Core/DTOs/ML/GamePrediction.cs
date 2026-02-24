namespace Moneyball.Core.DTOs.ML;

/// <summary>
/// ML.NET output class for game prediction.
/// Must match the output schema of trained binary classification models.
/// </summary>
public class GamePrediction
{
    /// <summary>
    /// Predicted label (true for home win, false for away win)
    /// </summary>
    public bool PredictedLabel { get; set; }

    /// <summary>
    /// Probability score (0.0 to 1.0)
    /// This is the raw score from the model
    /// </summary>
    public float Score { get; set; }

    /// <summary>
    /// Probability that the prediction is correct (0.0 to 1.0)
    /// For binary classification, this represents probability of the positive class
    /// </summary>
    public float Probability { get; set; }
}