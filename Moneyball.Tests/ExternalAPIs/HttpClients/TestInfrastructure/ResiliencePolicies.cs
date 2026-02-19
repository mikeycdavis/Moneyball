using Microsoft.Extensions.Http.Resilience;
using Polly;
using System.Net;

namespace Moneyball.Tests.ExternalAPIs.HttpClients.TestInfrastructure;

// ---------------------------------------------------------------------------
// Shared pipeline — mirrors your DI registration exactly.
// Keep this in ResiliencePolicies.cs in your main project.
// ---------------------------------------------------------------------------

public static class ResiliencePolicies
{
    public const int MaxRetries = 3;
    public const int BreakerThreshold = 5;

    public static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(1);

    public static void ConfigureResiliencePipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> pipeline,
        TimeProvider? timeProvider = null)
    {
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = MaxRetries,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = false, // disabled for deterministic tests
            Delay = BaseRetryDelay,
            ShouldHandle = args =>
            {
                if (args.Outcome.Result is { } response)
                    return ValueTask.FromResult(
                        (int)response.StatusCode >= 500 ||
                        response.StatusCode == HttpStatusCode.TooManyRequests);

                return ValueTask.FromResult(args.Outcome.Exception is not null);
            }
        });

        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 1.0,
            MinimumThroughput = BreakerThreshold,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = BreakDuration,
            ShouldHandle = args =>
            {
                if (args.Outcome.Result is { } response)
                    return ValueTask.FromResult(
                        (int)response.StatusCode >= 500 ||
                        response.StatusCode == HttpStatusCode.TooManyRequests);

                return ValueTask.FromResult(args.Outcome.Exception is not null);
            }
        });
    }
}