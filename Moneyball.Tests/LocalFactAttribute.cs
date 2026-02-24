using System.Runtime.CompilerServices;

namespace Moneyball.Tests;

public class LocalFactAttribute : FactAttribute
{
    public LocalFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) 
        : base(sourceFilePath, sourceLineNumber)
    {
        var isCI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"));

        if (isCI)
        {
            Skip = "Skipping local-only test on CI.";
        }
    }
}