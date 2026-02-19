using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Infrastructure.ExternalAPIs.Odds;
using Moneyball.Infrastructure.ExternalAPIs.SportsRadar;
using RichardSzalay.MockHttp;

namespace Moneyball.Tests.HttpClients.TestInfrastructure;

/// <summary>
/// Builds a ServiceProvider that wires the real service implementations
/// to a MockHttpMessageHandler and the shared resilience pipeline —
/// exactly as your DI registration does in production.
/// </summary>
internal static class ServiceProviderFactory
{
    // These match the defaults in your real service constructors
    private const string SportsRadarBase = "https://api.sportradar.com/nba/trial/v8/en";
    private const string TheOddsApiBase = "https://api.the-odds-api.com/v4";

    public static ServiceProvider Build(
        MockHttpMessageHandler mockHandler,
        FakeTimeProvider? fakeTime = null)
    {
        // Mirrors your appsettings.json structure
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "test-sports-api-key",
                ["SportsData:BaseUrl"] = SportsRadarBase,
                ["OddsAPI:ApiKey"] = "test-odds-api-key",
                ["OddsAPI:BaseUrl"] = TheOddsApiBase
            })
            .Build();

        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(); // NullLogger resolves for ILogger<T>

        services
            .AddHttpClient<ISportsDataService, SportsDataService>()
            .ConfigurePrimaryHttpMessageHandler(() => mockHandler)
            .AddResilienceHandler("sports-data-pipeline",
                (pipeline, _) => ResiliencePolicies.ConfigureResiliencePipeline(pipeline, fakeTime));

        services
            .AddHttpClient<IOddsDataService, OddsDataService>()
            .ConfigurePrimaryHttpMessageHandler(() => mockHandler)
            .AddResilienceHandler("odds-data-pipeline",
                (pipeline, _) => ResiliencePolicies.ConfigureResiliencePipeline(pipeline, fakeTime));

        return services.BuildServiceProvider();
    }
}