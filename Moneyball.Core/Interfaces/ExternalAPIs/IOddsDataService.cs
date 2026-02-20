using Moneyball.Core.DTOs.ExternalAPIs.Odds;

namespace Moneyball.Core.Interfaces.ExternalAPIs
{
    public interface IOddsDataService
    {
        Task<OddsResponse> GetOddsAsync(string sport, string region = "us", string market = "h2h");
    }
}
