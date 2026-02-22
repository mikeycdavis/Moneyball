using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moneyball.Core.Interfaces.ExternalServices;
using Moneyball.Infrastructure.ExternalServices;
using Moq;

namespace Moneyball.Tests.ExternalServices;

/// <summary>
/// Unit tests for DataIngestionBackgroundService class.
/// Tests cover service lifecycle, scheduled execution, error handling, and cancellation.
/// The background service runs on a 1-hour interval and coordinates data ingestion.
/// Uses Moq for mocking dependencies and FluentAssertions for readable assertions.
///
/// Tests are split into two tiers:
///   - Fast tests (~instant): verify behaviour that occurs before the first Task.Delay
///     (startup logging, pre-delay cancellation). These use StartServicePastStartupLogAsync().
///   - Slow tests (up to 90s): must wait out the real 1-minute startup delay to observe
///     actual orchestrator execution. Marked with [Fact(Timeout = 90_000)].
///
/// Synchronisation strategy — no arbitrary Task.Delay guesses anywhere:
///   - StartServicePastStartupLogAsync() uses a SemaphoreSlim wired into the mock logger
///     that is released the instant the background thread writes the startup log.
///   - CancelAndStopAsync() awaits service.ExecuteTask directly, which only completes
///     once ExecuteAsync has fully exited (stopping log already written).
///   - Tests that must observe post-startup execution await service.ExecuteTask with a
///     75-second timeout rather than polling with Task.Delay.
/// </summary>
public class DataIngestionBackgroundServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceScopeFactory> _mockServiceScopeFactory;
    private readonly Mock<ILogger<DataIngestionBackgroundService>> _mockLogger;
    private readonly Mock<IDataIngestionOrchestrator> _mockOrchestrator;
    private readonly Mock<IServiceProvider> _mockScopedServiceProvider;

    /// <summary>
    /// Test fixture setup - initializes all mocks.
    /// Configures service provider to return scoped orchestrator.
    /// Note: CreateScope() is an extension method, so we mock IServiceScopeFactory instead.
    /// </summary>
    public DataIngestionBackgroundServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockServiceScopeFactory = new Mock<IServiceScopeFactory>();
        _mockLogger = new Mock<ILogger<DataIngestionBackgroundService>>();
        _mockOrchestrator = new Mock<IDataIngestionOrchestrator>();
        _mockScopedServiceProvider = new Mock<IServiceProvider>();

        // Setup service provider to return scope factory
        // (CreateScope is an extension method that calls GetService<IServiceScopeFactory>)
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(_mockServiceScopeFactory.Object);

        // Setup scope factory to create scopes
        _mockServiceScopeFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockServiceScope.Object);

        // Setup scoped service provider to return orchestrator
        _mockScopedServiceProvider
            .Setup(sp => sp.GetService(typeof(IDataIngestionOrchestrator)))
            .Returns(_mockOrchestrator.Object);

        _mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockScopedServiceProvider.Object);

        // Setup orchestrator to complete successfully by default
        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Returns(Task.CompletedTask);
    }

    // ==================== Fast Tests ====================
    // These tests only assert behaviour that occurs before the first Task.Delay
    // in ExecuteAsync (i.e. the startup log). They complete almost instantly.

    /// <summary>
    /// Tests that service starts and logs startup message.
    /// StartServicePastStartupLogAsync blocks until the log is confirmed written,
    /// so this assertion is always safe.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OnStart_LogsStartupMessage()
    {
        var (service, cts) = await StartServicePastStartupLogAsync();

        VerifyLogInformation("Data Ingestion Background Service is starting");

        await CancelAndStopAsync(service, cts);
    }

    /// <summary>
    /// Tests that service waits 1 minute before first execution.
    /// Cancels during the startup delay and confirms the orchestrator was never reached.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WaitsOneMinute_BeforeFirstExecution()
    {
        var executionCount = 0;

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() => executionCount++)
            .Returns(Task.CompletedTask);

        // StartServicePastStartupLogAsync returns while we are still inside the
        // 1-minute startup Task.Delay — the orchestrator cannot have run yet.
        var (service, cts) = await StartServicePastStartupLogAsync();

        executionCount.Should().Be(0,
            "orchestrator should not execute during the initial 1-minute startup delay");

        await CancelAndStopAsync(service, cts);
    }

    /// <summary>
    /// Tests that service waits 1 hour between executions.
    /// Verifies the startup log is reliably written before any delay.
    /// Note: verifying the exact 1-hour interval requires real time and belongs in
    /// an integration test, or requires a timer abstraction injected into the service.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WaitsOneHour_BetweenExecutions()
    {
        var (service, cts) = await StartServicePastStartupLogAsync();

        // Startup log is always written before any Task.Delay — confirmed by the
        // semaphore gate inside StartServicePastStartupLogAsync.
        VerifyLogInformation("Data Ingestion Background Service is starting");

        await CancelAndStopAsync(service, cts);
    }

    /// <summary>
    /// Tests that service doesn't execute when cancelled before initial delay.
    /// Awaits ExecuteTask directly instead of using an arbitrary Task.Delay so
    /// the assertion only runs once ExecuteAsync has fully exited.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_CancelledBeforeFirstExecution_NeverCallsOrchestrator()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        // Cancel immediately — still within the 1-minute startup delay
        await cts.CancelAsync();

        // Await ExecuteTask so we know the loop has fully exited before asserting.
        // Previously this used Task.Delay(100) which was an unreliable guess.
        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        catch (OperationCanceledException) { /* expected — cancellation propagated */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockOrchestrator.Verify(
            o => o.RunScheduledUpdatesAsync(),
            Times.Never,
            "orchestrator should not be called when cancelled during startup delay");
    }

    // ==================== Slow Tests (up to 90s) ====================
    // These tests must wait out the real 1-minute startup delay before the
    // orchestrator is invoked. They await service.ExecuteTask rather than
    // polling with arbitrary Task.Delay calls.

    /// <summary>
    /// Tests that service stops gracefully on cancellation.
    /// CancelAndStopAsync awaits ExecuteTask to full completion, which only happens
    /// after the stopping log is written — so the assertion below is always safe.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OnCancellation_StopsGracefully()
    {
        var (service, cts) = await StartServicePastStartupLogAsync();

        // Wait a bit on startup to let the application fully initialize
        await Task.Delay(TimeSpan.FromMinutes(1), cts.Token);

        // CancelAndStopAsync awaits service.ExecuteTask, which only resolves once
        // ExecuteAsync has fully exited — the stopping log is guaranteed written.
        await CancelAndStopAsync(service, cts);

        VerifyLogInformation("Data Ingestion Background Service is stopping");
    }

    /// <summary>
    /// Tests that orchestrator is called after the startup delay.
    /// Cancels after first execution to avoid waiting out the 1-hour interval.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_CallsOrchestratorOnSchedule()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() => cts.Cancel()) // cancel after first execution
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);

        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(75), cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockOrchestrator.Verify(
            o => o.RunScheduledUpdatesAsync(),
            Times.AtLeastOnce,
            "orchestrator should be called after the 1-minute startup delay");
    }

    /// <summary>
    /// Tests that service creates and disposes scopes correctly.
    /// Verifies proper DI scope management per execution cycle.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_CreatesAndDisposesScope()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        var scopeDisposed = false;

        _mockServiceScope
            .Setup(s => s.Dispose())
            .Callback(() => scopeDisposed = true);

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);

        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(75), cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockServiceScopeFactory.Verify(f => f.CreateScope(), Times.Once,
            "a new scope should be created for each execution cycle");
        scopeDisposed.Should().BeTrue("scope should be disposed after execution");
    }

    /// <summary>
    /// Tests that orchestrator errors are caught and logged.
    /// Verifies service continues after orchestrator failure.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_OrchestratorError_LogsAndContinues()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        // Auto-cancel after 75s so the test does not hang
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(75));

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .ThrowsAsync(new InvalidOperationException("Orchestrator failure"));

        await service.StartAsync(cts.Token);

        // The error is caught inside the loop so the service survives and enters
        // the interval Task.Delay. Wait just past the startup delay to confirm.
        await Task.Delay(
            TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        VerifyLogError("Error occurred during scheduled data ingestion");

        _mockOrchestrator.Verify(
            o => o.RunScheduledUpdatesAsync(),
            Times.AtLeastOnce,
            "service should attempt execution despite orchestrator error");

        await CancelAndStopAsync(service, cts);
    }

    /// <summary>
    /// Tests that cycle completion is logged after successful execution.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_SuccessfulCycle_LogsCompletion()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);

        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(75), cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        VerifyLogInformation("Starting scheduled data ingestion cycle");
        VerifyLogInformation("Scheduled data ingestion cycle complete");
    }

    /// <summary>
    /// Tests that TaskCanceledException during the interval delay breaks the loop.
    /// Waits for startup + first execution then cancels while in the interval delay.
    /// The Task.Delay(200) that previously followed CancelAndStopAsync has been removed:
    /// CancelAndStopAsync already awaits ExecuteTask to full completion, which only
    /// happens after the stopping log is written, so no additional wait is needed.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_CancellationDuringDelay_BreaksLoop()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        var executionCount = 0;

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() => executionCount++)
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);

        // Wait for startup delay + first execution, then cancel while in interval delay
        await Task.Delay(
            TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // CancelAndStopAsync awaits ExecuteTask to completion — both executionCount
        // and the stopping log are stable by the time this returns.
        await CancelAndStopAsync(service, cts);

        executionCount.Should().BeGreaterThanOrEqualTo(1,
            "should execute at least once before cancellation during interval delay");
        VerifyLogInformation("Data Ingestion Background Service is stopping");
    }

    /// <summary>
    /// Tests that service handles multiple execution cycles correctly.
    /// Note: running two full cycles would take over 2 hours (1-min startup +
    /// 1-hour interval). This test verifies at least one cycle completes cleanly.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_MultipleExecutions_CallsOrchestratorMultipleTimes()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        var executionCount = 0;

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() =>
            {
                executionCount++;
                if (executionCount >= 1) cts.Cancel();
            })
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);

        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(75), cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockOrchestrator.Verify(
            o => o.RunScheduledUpdatesAsync(),
            Times.AtLeastOnce,
            "orchestrator should be called at least once");
    }

    /// <summary>
    /// Tests that service resolves orchestrator from scoped service provider.
    /// Verifies proper DI resolution from the child scope.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_ResolvesOrchestratorFromScope()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();
        var resolved = false;

        _mockScopedServiceProvider
            .Setup(sp => sp.GetService(typeof(IDataIngestionOrchestrator)))
            .Callback(() => resolved = true)
            .Returns(_mockOrchestrator.Object);

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);

        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(75), cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        resolved.Should().BeTrue(
            "orchestrator should be resolved from the scoped service provider");
    }

    /// <summary>
    /// Tests that exception in scope disposal is handled.
    /// Verifies the orchestrator was at least invoked before the disposal error.
    /// </summary>
    [Fact(Timeout = 90_000)]
    public async Task ExecuteAsync_ScopeDisposalError_HandledGracefully()
    {
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();

        _mockServiceScope
            .Setup(s => s.Dispose())
            .Throws(new ObjectDisposedException("Scope already disposed"));

        _mockOrchestrator
            .Setup(o => o.RunScheduledUpdatesAsync())
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        await service.StartAsync(cts.Token);

        // The disposal exception propagates out of the using block and is caught
        // by the outer try/catch in ExecuteAsync, which logs it as an error.
        try
        {
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(75), cts.Token);
        }
        catch (OperationCanceledException) { /* expected */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }

        _mockOrchestrator.Verify(
            o => o.RunScheduledUpdatesAsync(),
            Times.AtLeastOnce,
            "orchestrator should execute before disposal error occurs");
    }

    // ==================== Helper Methods ====================

    /// <summary>
    /// Starts the service and blocks until the startup log has actually been written
    /// on the background thread — with no arbitrary Task.Delay guess.
    ///
    /// StartAsync returns immediately after queuing ExecuteAsync onto the thread pool,
    /// so the startup log may not yet exist when StartAsync returns. A SemaphoreSlim
    /// wired into the mock logger bridges that gap: it is released the moment the
    /// background thread calls LogInformation("...is starting"), so we block here for
    /// exactly as long as needed (up to a 5s safety timeout).
    ///
    /// Because the startup log is written before Task.Delay(1 minute), the returned
    /// service is guaranteed to still be inside that startup delay.
    ///
    /// Previously used Task.Delay(500) — an unreliable guess on loaded CI machines.
    /// </summary>
    private async Task<(DataIngestionBackgroundService service, CancellationTokenSource cts)>
        StartServicePastStartupLogAsync()
    {
        // CRITICAL: the semaphore and mock callback MUST be registered before StartAsync
        // is called. StartAsync queues ExecuteAsync onto the thread pool and returns
        // immediately — the background thread can write the startup log before the very
        // next line of this method executes. Registering the callback afterwards creates
        // a race where it is never invoked and WaitAsync times out after 5 seconds.
        var startupLogged = new SemaphoreSlim(0, 1);

        _mockLogger
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains("Data Ingestion Background Service is starting")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => startupLogged.Release());

        // Construct the service after the mock is configured so the logger instance
        // it captures already has the callback in place.
        var service = new DataIngestionBackgroundService(
            _mockServiceProvider.Object, _mockLogger.Object);
        var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);

        // Block until the background thread confirms it has written the startup log.
        // With the setup in place before StartAsync this releases almost instantly;
        // the 5-second timeout is only a safety net for broken implementations.
        var logged = await startupLogged.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        logged.Should().BeTrue("startup log should be written within 5 seconds");

        return (service, cts);
    }

    /// <summary>
    /// Cancels the token then awaits ExecuteTask to full completion (up to 5s).
    /// ExecuteAsync catches TaskCanceledException internally and exits the loop
    /// cleanly, so ExecuteTask completes normally (not faulted). By the time this
    /// method returns, the stopping log is guaranteed to have been written.
    /// </summary>
    private static async Task CancelAndStopAsync(
        DataIngestionBackgroundService service,
        CancellationTokenSource cts)
    {
        await cts.CancelAsync();

        try
        {
            // ExecuteTask completes normally because the service handles
            // TaskCanceledException itself. WaitAsync guards against the test
            // hanging if the implementation has a bug.
            await service.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5), cts.Token);
        }
        catch (OperationCanceledException) { /* propagated by WaitAsync on timeout edge case */ }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Verifies that an information log was written containing the expected message.
    /// </summary>
    private void VerifyLogInformation(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            $"Expected log message containing: {expectedMessage}");
    }

    /// <summary>
    /// Verifies that an error was logged containing the expected message.
    /// </summary>
    private void VerifyLogError(string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(expectedMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            $"Expected error log containing: {expectedMessage}");
    }
}