using Moneyball.Core.Entities;

namespace Moneyball.Core.Interfaces
{
    public interface IMoneyballRepository : IDisposable
    {
        //IGameRepository Games { get; }
        //ITeamRepository Teams { get; }
        //IGameOddsRepository GameOdds { get; }
        //IPredictionRepository Predictions { get; }
        //IModelRepository Models { get; }
        IRepository<Sport> Sports { get; }
        IRepository<TeamStatistic> TeamStatistics { get; }
        IRepository<ModelPerformance> ModelPerformances { get; }
        IRepository<BettingRecommendation> BettingRecommendations { get; }

        Task<int> SaveChangesAsync();
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
