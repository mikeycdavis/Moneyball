using Moneyball.Core.DTOs;
using Moneyball.Core.Entities;
using Moneyball.Core.Interfaces.Repositories;
//using Moneyball.Infrastructure.ML;

namespace Moneyball.Infrastructure.Services
{
    public class BettingRecommendationService
    {
        private readonly IGameRepository _gameRepository;
        private readonly IPredictionRepository _predictionRepository;
        private readonly IOddsRepository _oddsRepository;
        //private readonly KellyCriterionCalculator _kellyCalculator;

        //public async Task<BettingRecommendationResponse> GetRecommendationsAsync(
        //    BettingRecommendationRequest request)
        //{
        //    // 1. Get upcoming games
        //    var upcomingGames = await _gameRepository.GetUpcomingGamesAsync(
        //        request.SportId);

        //    // 2. Get predictions from specified models (or all active)
        //    var predictions = await GetPredictionsForGamesAsync(
        //        upcomingGames, request.ModelIds);

        //    // 3. Calculate edge for each prediction
        //    var opportunities = await CalculateEdgesAsync(predictions);

        //    // 4. Filter by minimum edge and confidence
        //    var qualified = opportunities
        //        .Where(o => o.Edge >= request.MinEdge &&
        //                   o.Confidence >= request.MinConfidence)
        //        .OrderByDescending(o => o.Edge) // or Confidence * Edge
        //        .Take(request.TopN)
        //        .ToList();

        //    // 5. Calculate stakes using Kelly Criterion
        //    var recommendations = CalculateStakes(qualified, request.Bankroll);

        //    return new BettingRecommendationResponse
        //    {
        //        Recommendations = recommendations,
        //        TotalStake = recommendations.Sum(r => r.RecommendedStake),
        //        RemainingBankroll = request.Bankroll -
        //            recommendations.Sum(r => r.RecommendedStake)
        //    };
        //}

        //private async Task<List<BettingOpportunity>> CalculateEdgesAsync(
        //    List<Prediction> predictions)
        //{
        //    var opportunities = new List<BettingOpportunity>();

        //    foreach (var prediction in predictions)
        //    {
        //        var odds = await _oddsRepository.GetLatestOddsAsync(
        //            prediction.GameId);

        //        if (odds == null) continue;

        //        // Calculate implied probability from American odds
        //        var homeImplied = ConvertOddsToImpliedProbability(
        //            odds.HomeMoneyline);
        //        var awayImplied = ConvertOddsToImpliedProbability(
        //            odds.AwayMoneyline);

        //        // Edge = Model Probability - Implied Probability
        //        var homeEdge = prediction.PredictedHomeWinProbability - homeImplied;
        //        var awayEdge = prediction.PredictedAwayWinProbability - awayImplied;

        //        // Take the best edge
        //        if (homeEdge > awayEdge && homeEdge > 0)
        //        {
        //            opportunities.Add(new BettingOpportunity
        //            {
        //                Prediction = prediction,
        //                Edge = homeEdge,
        //                RecommendedSide = "Home",
        //                WinProbability = prediction.PredictedHomeWinProbability,
        //                Odds = odds.HomeMoneyline
        //            });
        //        }
        //        else if (awayEdge > 0)
        //        {
        //            opportunities.Add(new BettingOpportunity
        //            {
        //                Prediction = prediction,
        //                Edge = awayEdge,
        //                RecommendedSide = "Away",
        //                WinProbability = prediction.PredictedAwayWinProbability,
        //                Odds = odds.AwayMoneyline
        //            });
        //        }
        //    }

        //    return opportunities;
        //}

        private decimal ConvertOddsToImpliedProbability(decimal americanOdds)
        {
            if (americanOdds > 0)
                return 100m / (americanOdds + 100m);
            else
                return Math.Abs(americanOdds) / (Math.Abs(americanOdds) + 100m);
        }
    }
}
