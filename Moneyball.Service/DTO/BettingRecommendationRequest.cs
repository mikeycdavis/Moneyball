namespace Moneyball.Service.DTO
{
    public class BettingRecommendationRequest
    {
        public decimal Bankroll { get; set; }
        public int TopN { get; set; } = 5;
        public decimal MinEdge { get; set; } = 0.05m; // 5% minimum edge
        public decimal MinConfidence { get; set; } = 0.6m;
        public int? SportId { get; set; }
        public List<int> ModelIds { get; set; } = []; // Use specific models
    }
}
