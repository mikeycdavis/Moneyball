using Microsoft.AspNetCore.Mvc;
using Moneyball.Core.DTOs;
using Moneyball.Infrastructure.Services;

namespace Moneyball.API.Controllers
{
    // Example request:
    // POST /api/bettingrecommendations
    // {
    //   "bankroll": 10000,
    //   "topN": 5,
    //   "minEdge": 0.05,
    //   "minConfidence": 0.65,
    //   "sportId": 1,
    //   "modelIds": [3, 5, 7]  // Use ensemble of models 3, 5, 7
    // }
    //[ApiController]
    //[Route("api/[controller]")]
    //public class BettingRecommendationsController(BettingRecommendationService service) : ControllerBase
    //{
    //    [HttpPost]
    //    public async Task<ActionResult<BettingRecommendationResponse>> GetRecommendations([FromBody] BettingRecommendationRequest request)
    //    {
    //        if (request.Bankroll <= 0)
    //            return BadRequest("Bankroll must be positive");

    //        var response = await service.GetRecommendationsAsync(request);
    //        return Ok(response);
    //    }
    //}
}
