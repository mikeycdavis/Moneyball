namespace Moneyball.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when model execution fails
    /// </summary>
    public class ModelExecutionException : Exception
    {
        public string? ModelId { get; set; }
        public string? ModelType { get; set; }

        public ModelExecutionException(string message) : base(message) { }

        public ModelExecutionException(string message, Exception innerException)
            : base(message, innerException) { }

        public ModelExecutionException(string message, string modelId, string modelType)
            : base(message)
        {
            ModelId = modelId;
            ModelType = modelType;
        }

        public ModelExecutionException(string message, string modelId, string modelType, Exception innerException)
            : base(message, innerException)
        {
            ModelId = modelId;
            ModelType = modelType;
        }
    }
}
