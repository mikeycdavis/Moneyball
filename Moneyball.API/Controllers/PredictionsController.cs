using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moneyball.Infrastructure.NBA;

namespace Moneyball.API.Controllers
{
    [ApiController]
    [Route("api/predictions")]
    public class PredictionsController : ControllerBase
    {
        private readonly NbaEdgeDbContext _db;

        public PredictionsController(NbaEdgeDbContext db)
        {
            _db = db;
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetTodayPredictions()
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var predictions = await _db.Predictions
                .Where(p => p.CreatedAt.Date == DateTime.UtcNow.Date)
                .ToListAsync();

            return Ok(predictions);
        }
    }
}
