using Moneyball.API;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database Context
builder.Services.ConfigureDatabase(builder.Configuration);

// Register Repositories
builder.Services.ConfigureRepositories();

// HTTP Clients with Polly retry policies
builder.Services.ConfigureHttpClients(builder.Configuration);

// Register Data Services
builder.Services.ConfigureServices();

// Background Services
builder.Services.ConfigureBackgroundServices(builder.Configuration);

// CORS (if needed for frontend)
builder.Services.ConfigureCors();

// Swagger
builder.Services.ConfigureSwagger();

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
app.Services.MigrateDatabase();

app.Run();

// Make Program accessible for testing
public partial class Program { }
