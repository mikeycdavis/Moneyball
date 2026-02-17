using Microsoft.EntityFrameworkCore.Storage;
using Moneyball.Core.Entities;
using Moneyball.Core.Interfaces;

namespace Moneyball.Infrastructure.Repositories;

public interface IMoneyballRepository : IDisposable
{
    IGameRepository Games { get; }
    ITeamRepository Teams { get; }
    IGameOddsRepository GameOdds { get; }
    IPredictionRepository Predictions { get; }
    IModelRepository Models { get; }
    IRepository<Sport> Sports { get; }
    IRepository<TeamStatistic> TeamStatistics { get; }
    IRepository<ModelPerformance> ModelPerformances { get; }
    IRepository<BettingRecommendation> BettingRecommendations { get; }

    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}

public class MoneyballRepository : IMoneyballRepository
{
    private readonly MoneyballDbContext _context;
    private IDbContextTransaction? _transaction;

    public MoneyballRepository(MoneyballDbContext context)
    {
        _context = context;

        Games = new GameRepository(_context);
        Teams = new TeamRepository(_context);
        GameOdds = new GameOddsRepository(_context);
        Predictions = new PredictionRepository(_context);
        Models = new ModelRepository(_context);
        Sports = new Repository<Sport>(_context);
        TeamStatistics = new Repository<TeamStatistic>(_context);
        ModelPerformances = new Repository<ModelPerformance>(_context);
        BettingRecommendations = new Repository<BettingRecommendation>(_context);
    }

    public IGameRepository Games { get; }
    public ITeamRepository Teams { get; }
    public IGameOddsRepository GameOdds { get; }
    public IPredictionRepository Predictions { get; }
    public IModelRepository Models { get; }
    public IRepository<Sport> Sports { get; }
    public IRepository<TeamStatistic> TeamStatistics { get; }
    public IRepository<ModelPerformance> ModelPerformances { get; }
    public IRepository<BettingRecommendation> BettingRecommendations { get; }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
            }
        }
        catch
        {
            await RollbackTransactionAsync();
            throw;
        }
        finally
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}