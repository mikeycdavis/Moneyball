using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moneyball.Core.Interfaces.ExternalAPIs;
using Moneyball.Infrastructure.ExternalAPIs.Odds;
using Moneyball.Infrastructure.ExternalAPIs.SportsRadar;
using RichardSzalay.MockHttp;

namespace Moneyball.Tests.HttpClients.TestInfrastructure;

/// <summary>
/// Directly instantiates a SportsDataService with a bare HttpClient and
/// inline config — for typed client behaviour tests that don't need policies.
/// </summary>
internal static class DirectFactory
{
    private const string SportsRadarBase = "https://api.sportradar.com/nba/trial/v8/en";
    private const string TheOddsApiBase = "https://api.the-odds-api.com/v4";

    public static (ISportsDataService service, IConfiguration config) BuildSportsDataService(
        MockHttpMessageHandler mockHandler)
    {
        var config = BuildConfig();
        var client = mockHandler.ToHttpClient();
        var service = new SportsDataService(
            client,
            config,
            NullLogger<SportsDataService>.Instance);
        return (service, config);
    }

    public static (IOddsDataService service, IConfiguration config) BuildOddsDataService(
        MockHttpMessageHandler mockHandler)
    {
        var config = BuildConfig();
        var client = mockHandler.ToHttpClient();
        var service = new OddsDataService(
            client,
            config,
            NullLogger<OddsDataService>.Instance);
        return (service, config);
    }

    private static IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData:ApiKey"] = "test-sports-api-key",
                ["SportsData:BaseUrl"] = SportsRadarBase,
                ["OddsAPI:ApiKey"] = "test-odds-api-key",
                ["OddsAPI:BaseUrl"] = TheOddsApiBase
            })
            .Build();
}