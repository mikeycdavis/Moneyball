using Microsoft.EntityFrameworkCore;
using Moneyball.Core.Interfaces;
using Moneyball.Infrastructure.Repositories;
using Polly;
using Polly.Extensions.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database Context
builder.Services.AddDbContext<MoneyballDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)));

// Register Repositories
//builder.Services.AddScoped<IGameOddsRepository, GameOddsRepository>();
//builder.Services.AddScoped<IRepository<GameOdds>, Repository<GameOdds>>();
//builder.Services.AddScoped<IGameRepository, GameRepository>();
//builder.Services.AddScoped<IModelRepository, ModelRepository>();
builder.Services.AddScoped<IMoneyballRepository, MoneyballRepository>();
//builder.Services.AddScoped<IPredictionRepository, PredictionRepository>();
//builder.Services.AddScoped<ITeamRepository, TeamRepository>();

// HTTP Clients with Polly retry policies
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

//builder.Services.AddHttpClient<ISportsDataService, SportsDataService>()
//    .AddPolicyHandler(retryPolicy)
//    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

//builder.Services.AddHttpClient<IOddsDataService, OddsDataService>()
//    .AddPolicyHandler(retryPolicy)
//    .SetHandlerLifetime(TimeSpan.FromMinutes(5));

//// Register Data Services
//builder.Services.AddScoped<IDataIngestionService, DataIngestionService>();
//builder.Services.AddScoped<IDataIngestionOrchestrator, DataIngestionOrchestrator>();

// Background Services
if (builder.Configuration.GetValue<bool>("DataIngestion:EnableBackgroundService"))
{
    //builder.Services.AddHostedService<DataIngestionBackgroundService>();
}

// CORS (if needed for frontend)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policyBuilder =>
        {
            policyBuilder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Ensure database is created (for development)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MoneyballDbContext>();
    try
    {
        var pendingMigrations = db.Database.GetPendingMigrations();

        if (pendingMigrations.Any())
        {
            db.Database.Migrate();
            Console.WriteLine("Database migration completed successfully");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error during database migration: {ex.Message}");
    }
}

app.Run();

// Make Program accessible for testing
public partial class Program { }