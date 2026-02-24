using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moneyball.Core.DTOs;
using Moneyball.Core.Entities;
using Moneyball.Core.Enums;
using Moneyball.Core.Exceptions;
using Moneyball.Infrastructure.ML;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Moneyball.Tests.ML;

public class PythonModelExecutorTests
{
    // ---------------------------------------------------------------------------
    // Helpers & shared setup
    // ---------------------------------------------------------------------------

    private const string ServiceUrl = "http://ml-service";

    /// <summary>
    /// Builds a real HttpClient whose inner handler is mocked, so we can
    /// intercept SendAsync without hitting the network.
    /// </summary>
    private static HttpClient CreateHttpClient(
        HttpStatusCode statusCode,
        object? responseBody = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        var jsonBody = responseBody is null
            ? "null"
            : JsonSerializer.Serialize(responseBody);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
            });

        return new HttpClient(handlerMock.Object);
    }

    /// <summary>
    /// Builds a minimal IConfiguration containing only the service URL key.
    /// </summary>
    private static IConfiguration BuildConfig(string? url = ServiceUrl)
    {
        var data = new Dictionary<string, string?> { ["PythonMLService:Url"] = url };
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    /// <summary>
    /// Returns a valid Model and feature dictionary to use as default test inputs.
    /// </summary>
    private static (Model model, Dictionary<string, object> features) DefaultInputs() =>
    (
        new Model { Name = "WinPredictor", Version = "1.0", Type = ModelType.Python },
        new Dictionary<string, object> { ["goals"] = 3, ["shots"] = 12 }
    );

    // ---------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenHttpClientIsNull()
    {
        var act = () => new PythonModelExecutor(null!, BuildConfig());

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Constructor_ThrowsInvalidOperationException_WhenUrlConfigIsMissing()
    {
        // Pass a config that has no PythonMLService:Url key at all
        var emptyConfig = new ConfigurationBuilder().Build();
        var client = CreateHttpClient(HttpStatusCode.OK);

        var act = () => new PythonModelExecutor(client, emptyConfig);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PythonMLService:Url*");
    }

    // ---------------------------------------------------------------------------
    // SupportedModelType / CanExecute
    // ---------------------------------------------------------------------------

    [Fact]
    public void SupportedModelType_ReturnsPython()
    {
        var client = CreateHttpClient(HttpStatusCode.OK);
        var executor = new PythonModelExecutor(client, BuildConfig());

        executor.SupportedModelType.Should().Be(ModelType.Python);
    }

    [Fact]
    public void CanExecute_ReturnsTrue_ForPythonModel()
    {
        var client = CreateHttpClient(HttpStatusCode.OK);
        var executor = new PythonModelExecutor(client, BuildConfig());
        var model = new Model { Type = ModelType.Python };

        executor.CanExecute(model).Should().BeTrue();
    }

    [Fact]
    public void CanExecute_ReturnsFalse_ForNonPythonModel()
    {
        var client = CreateHttpClient(HttpStatusCode.OK);
        var executor = new PythonModelExecutor(client, BuildConfig());

        // Any model type that is not Python should be rejected
        var model = new Model { Type = ModelType.MLNet };

        executor.CanExecute(model).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------
    // ExecuteAsync — argument validation
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsArgumentNullException_WhenModelIsNull()
    {
        var client = CreateHttpClient(HttpStatusCode.OK);
        var executor = new PythonModelExecutor(client, BuildConfig());
        var (_, features) = DefaultInputs();

        await FluentActions.Awaiting(() => executor.ExecuteAsync(null!, features))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("model");
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsArgumentNullException_WhenFeaturesIsNull()
    {
        var client = CreateHttpClient(HttpStatusCode.OK);
        var executor = new PythonModelExecutor(client, BuildConfig());
        var (model, _) = DefaultInputs();

        var act = () => executor.ExecuteAsync(model, null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("features");
    }

    // ---------------------------------------------------------------------------
    // ExecuteAsync — happy path
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ReturnsPredictionResult_OnSuccess()
    {
        var expected = new PredictionResult { HomeWinProbability = 0.72m, Confidence = 0.85m };
        var client = CreateHttpClient(HttpStatusCode.OK, expected);
        var executor = new PythonModelExecutor(client, BuildConfig());
        var (model, features) = DefaultInputs();

        var result = await executor.ExecuteAsync(model, features);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ExecuteAsync_PostsToCorrectEndpoint_WithCorrectModelName()
    {
        var expected = new PredictionResult { HomeWinProbability = 0.5m, Confidence = 0.9m };
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // Capture the outgoing request so we can assert against it
        HttpRequestMessage? capturedRequest = null;

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(expected)
            });

        var executor = new PythonModelExecutor(new HttpClient(handlerMock.Object), BuildConfig());
        var (model, features) = DefaultInputs();

        await executor.ExecuteAsync(model, features);

        // Verify the request hit the right URL
        capturedRequest!.RequestUri.Should().Be($"{ServiceUrl}/predict");
        capturedRequest.Method.Should().Be(HttpMethod.Post);

        // Verify model_name is formed as Name_Version
        var body = await capturedRequest.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("WinPredictor_1.0");
    }

    // ---------------------------------------------------------------------------
    // ExecuteAsync — non-2xx responses
    // ---------------------------------------------------------------------------

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ExecuteAsync_ThrowsMeaningfulException_OnNon2xxResponse(HttpStatusCode statusCode)
    {
        // Flask error responses typically include a plain-text or JSON body
        var errorBody = new { error = "model not found" };
        var client = CreateHttpClient(statusCode, errorBody);
        var executor = new PythonModelExecutor(client, BuildConfig());
        var (model, features) = DefaultInputs();

        var exception = await FluentActions.Awaiting(() => executor.ExecuteAsync(model, features))
            .Should().ThrowAsync<ModelExecutionException>();

        // The exception message should include the status code and the model name
        // so that the caller can diagnose the failure without digging through logs
        exception.WithMessage($"*{(int)statusCode}*")
            .And.Message.Should().Contain("WinPredictor_1.0");
    }

    [Fact]
    public async Task ExecuteAsync_IncludesResponseBody_InExceptionMessage_OnNon2xxResponse()
    {
        // Ensures the Flask error payload surfaces in the exception, not just the status code
        var client = CreateHttpClient(HttpStatusCode.UnprocessableEntity,
            new { error = "missing feature: xG" });
        var executor = new PythonModelExecutor(client, BuildConfig());
        var (model, features) = DefaultInputs();

        await FluentActions.Awaiting(() => executor.ExecuteAsync(model, features))
            .Should().ThrowAsync<ModelExecutionException>()
            .WithMessage("*missing feature: xG*");
    }

    // ---------------------------------------------------------------------------
    // ExecuteAsync — network / timeout failures
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsModelExecutionException_WhenServiceUnreachable()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // Simulate a connection-refused / DNS failure
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var executor = new PythonModelExecutor(new HttpClient(handlerMock.Object), BuildConfig());
        var (model, features) = DefaultInputs();

        var exception = await FluentActions.Awaiting(() => executor.ExecuteAsync(model, features))
            .Should().ThrowAsync<ModelExecutionException>()
            .WithMessage("*Failed to reach*");
        exception.Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsModelExecutionException_OnTimeout()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        // HttpClient raises TaskCanceledException when its timeout elapses
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timed out"));

        var executor = new PythonModelExecutor(new HttpClient(handlerMock.Object), BuildConfig());
        var (model, features) = DefaultInputs();

        var exception = await FluentActions.Awaiting(() => executor.ExecuteAsync(model, features))
            .Should().ThrowAsync<ModelExecutionException>()
            .WithMessage("*timed out*");
        exception.Which.InnerException.Should().BeOfType<TaskCanceledException>();
    }

    // ---------------------------------------------------------------------------
    // ExecuteAsync — deserialisation failures
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ThrowsModelExecutionException_WhenResponseIsNotValidJson()
    {
        // Flask might return an HTML error page or a Python traceback as plain text
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<!DOCTYPE html><h1>Traceback</h1>",
                    System.Text.Encoding.UTF8, "application/json")
            });

        var executor = new PythonModelExecutor(new HttpClient(handlerMock.Object), BuildConfig());
        var (model, features) = DefaultInputs();

        var exception = await FluentActions.Awaiting(() => executor.ExecuteAsync(model, features))
            .Should().ThrowAsync<ModelExecutionException>()
            .WithMessage("*deserializ*");
        exception.Which.InnerException.Should().BeOfType<JsonException>();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsModelExecutionException_WhenResponseBodyIsNull()
    {
        // A 200 with a literal JSON null body should produce a clear error,
        // not a NullReferenceException bubbling up from the caller
        var client = CreateHttpClient(HttpStatusCode.OK, responseBody: null);
        var executor = new PythonModelExecutor(client, BuildConfig());
        var (model, features) = DefaultInputs();

        await FluentActions.Awaiting(() => executor.ExecuteAsync(model, features))
            .Should().ThrowAsync<ModelExecutionException>()
            .WithMessage("*empty response*");
    }
}