using Moneyball.Core.Entities;

namespace Moneyball.Core.DTOs
{
    public class BettingRecommendationResponse
    {
        public List<BettingRecommendation> Recommendations { get; set; } = [];
        public decimal TotalStake { get; set; }
        public decimal RemainingBankroll { get; set; }
    }
}
