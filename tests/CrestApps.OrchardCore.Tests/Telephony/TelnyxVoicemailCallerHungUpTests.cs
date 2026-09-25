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
/// A caller who hangs up during the voicemail greeting.
/// </summary>
/// <remarks>
/// The greeting is played, and the recording started when it ends. A caller who hangs up during the greeting has left
/// before either can reach them, and Telnyx refuses the command with 422 "Call has already ended" (90018). That is the
/// caller choosing not to leave a message, an ordinary outcome, and it put an ERROR in the log every time.
/// </remarks>
public sealed class TelnyxVoicemailCallerHungUpTests
{
    private const string AlreadyEndedBody = """{"errors":[{"code":"90018","title":"Call has already ended","detail":"This call is no longer active and can't receive commands."}]}""";

    [Fact]
    public async Task StartingTheRecordingAfterTheCallerHungUp_IsNotLoggedAsAnError()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, AlreadyEndedBody);
        var logger = new RecordingLogger<TelnyxVoicemailRecordingStarter>();
        var starter = new TelnyxVoicemailRecordingStarter(CreateApiClient(handler), CreateOptionsMonitor(), logger);

        // Act
        var started = await starter.StartAsync("ctrl-1", "interaction-1", "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task ARecordingRefusedForAnyOtherReason_IsStillLoggedAsAnError()
    {
        // Arrange
        // Only the caller having left is expected; any other refusal loses a message the caller is trying to leave.
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90015","title":"Invalid call control ID"}]}""");
        var logger = new RecordingLogger<TelnyxVoicemailRecordingStarter>();
        var starter = new TelnyxVoicemailRecordingStarter(CreateApiClient(handler), CreateOptionsMonitor(), logger);

        // Act
        var started = await starter.StartAsync("ctrl-1", "interaction-1", "user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(started);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task AGreetingRefusedBecauseTheCallerHungUp_IsNotLoggedAsAnError()
    {
        // Arrange
        // The answer succeeds, and the caller is gone by the time the greeting is asked for.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""")
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, AlreadyEndedBody);
        var logger = new RecordingLogger<TelnyxTelephonyProvider>();
        var provider = CreateProvider(handler, logger);

        // Act
        var result = await provider.SendToVoicemailAsync(
            new CallReference
            {
                CallId = "ctrl-1",
                Metadata = new Dictionary<string, object> { ["interactionId"] = "interaction-1" },
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Contains(handler.Requests, request => request.Path.Contains("/actions/speak", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    private static TelnyxOptions CreateOptions()
        => new()
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
        };

    private static IOptionsMonitor<TelnyxOptions> CreateOptionsMonitor()
    {
        var optionsMonitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        optionsMonitor.SetupGet(x => x.CurrentValue).Returns(CreateOptions());

        return optionsMonitor.Object;
    }

    private static TelnyxApiClient CreateApiClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        return new TelnyxApiClient(
            httpClient,
            new OptionsWrapper<TelnyxOptions>(CreateOptions()),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);
    }

    private static TelnyxTelephonyProvider CreateProvider(RecordingHttpMessageHandler handler, ILogger<TelnyxTelephonyProvider> logger)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new TelnyxTelephonyProvider(
            CreateApiClient(handler),
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            clock.Object,
            logger,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            CreateOptionsMonitor());
    }

    private sealed class RecordingLogger<T> : ILogger<T>
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
