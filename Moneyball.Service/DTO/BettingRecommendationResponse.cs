using Moneyball.Data.Entities;

namespace Moneyball.Service.DTO
{
    public class BettingRecommendationResponse
    {
        public List<BettingRecommendation> Recommendations { get; set; } = [];
        public decimal TotalStake { get; set; }
        public decimal RemainingBankroll { get; set; }
    }
}
