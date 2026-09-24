using System.Net;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Hanging up a Telnyx call that has already ended.
/// </summary>
/// <remarks>
/// A bridged call is ended when either leg hangs up, so the platform routinely asks Telnyx to hang up a leg that
/// the other party's hangup has already taken down. Telnyx refuses that with 422 "Call has already ended" (90018).
/// The call being over is exactly what a hangup is for, so the refusal is the goal reached, not an error: logging it
/// as one put an ERROR in the log for every ordinary call ending.
/// </remarks>
public sealed class TelnyxHangupAlreadyEndedTests
{
    private const string AlreadyEndedBody = """{"errors":[{"code":"90018","title":"Call has already ended","detail":"This call is no longer active and can't receive commands."}]}""";

    [Fact]
    public async Task HangingUpACallThatAlreadyEnded_Succeeds_WithoutLoggingAnError()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, AlreadyEndedBody);
        var logger = new RecordingLogger();
        var provider = CreateProvider(handler, logger);

        // Act
        var result = await provider.HangupAsync(new CallReference { CallId = "ctrl-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(CallState.Disconnected, result.Call?.State);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task RejectingACallThatAlreadyEnded_Succeeds_WithoutLoggingAnError()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, AlreadyEndedBody);
        var logger = new RecordingLogger();
        var provider = CreateProvider(handler, logger);

        // Act
        var result = await provider.RejectAsync(new CallReference { CallId = "ctrl-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task AHangupRefusedForAnyOtherReason_IsStillAFailure_AndLoggedAsAnError()
    {
        // Arrange
        // Only "the call is already over" means the hangup's goal is met; any other refusal leaves a live call up.
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90015","title":"Invalid call control ID"}]}""");
        var logger = new RecordingLogger();
        var provider = CreateProvider(handler, logger);

        // Act
        var result = await provider.HangupAsync(new CallReference { CallId = "ctrl-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AnAnswerRefusedBecauseTheCallAlreadyEnded_IsStillAFailure()
    {
        // Arrange
        // An answer on a call that has ended has not done what it was asked; only ending a call is satisfied by it.
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, AlreadyEndedBody);
        var provider = CreateProvider(handler, new RecordingLogger());

        // Act
        var result = await provider.AnswerAsync(new CallReference { CallId = "ctrl-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    private static TelnyxTelephonyProvider CreateProvider(RecordingHttpMessageHandler handler, ILogger<TelnyxTelephonyProvider> logger)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
        };

        var apiClient = new TelnyxApiClient(
            httpClient,
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        var optionsMonitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        optionsMonitor.SetupGet(x => x.CurrentValue).Returns(options);

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new TelnyxTelephonyProvider(
            apiClient,
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            clock.Object,
            logger,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            optionsMonitor.Object);
    }

    private sealed class RecordingLogger : ILogger<TelnyxTelephonyProvider>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
