using System.Reflection;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.EventGrid;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Models;
using CrestApps.OrchardCore.Omnichannel.EventGrid.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.EventGrid;

/// <summary>
/// The Azure Event Grid webhook. The endpoint is an internal minimal-API handler, so these tests reach its handler and
/// its background inbound-SMS step by reflection and drive them with the same shell-scope doubles the Telephony hub
/// tests use. Each test names the bug it guards.
/// </summary>
public sealed class AzureEventGridEndpointTests
{
    private const string SasKey = "test-sas-key";

    private static readonly DateTime _nowUtc = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Type _endpointType = typeof(EventGridEventMapper).Assembly.GetType("AzureEventGridEndpoint", throwOnError: true);

    // Event Grid only activates a webhook subscription once the endpoint echoes the validation code back in exactly this
    // shape; any other answer leaves the subscription unvalidated and no text is ever delivered.
    [Fact]
    public async Task HandleAsync_SubscriptionValidation_AnswersWithTheValidationCode()
    {
        // Arrange
        using var harness = new Harness();
        var context = Harness.CreateRequest(ValidationEvent("code-123"));

        // Act
        var result = await harness.HandleAsync(context);
        await result.ExecuteAsync(context);

        // Assert
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("code-123", document.RootElement.GetProperty("validationResponse").GetString());
        Assert.Single(document.RootElement.EnumerateObject());
        Assert.Empty(harness.Handled);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task HandleAsync_SubscriptionValidationWithoutACode_IsABadRequest(string validationCode)
    {
        // Arrange
        using var harness = new Harness();
        var context = Harness.CreateRequest(ValidationEvent(validationCode));

        // Act
        var result = await harness.HandleAsync(context);

        // Assert
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
        Assert.Contains(harness.Logger.At(LogLevel.Warning), message => message.Contains("carried no validation code"));
    }

    // Bug: an ACS text arrived on the "Unknown" channel under its raw event type. It must reach the handlers as the SMS
    // received event, keyed by the provider's message id, with the provider's receive time.
    [Fact]
    public async Task HandleAsync_SmsReceived_RaisesTheSmsReceivedEventWithTheProviderReceiveTime()
    {
        // Arrange
        using var harness = new Harness();
        var receivedUtc = _nowUtc.AddMinutes(-3);
        var context = Harness.CreateRequest(SmsReceivedEvent("msg-1", receivedUtc));

        // Act
        var result = await harness.HandleAsync(context);
        var handled = await harness.WaitForHandledAsync();

        // Assert
        Assert.IsType<Ok>(result);
        Assert.Equal("msg-1", handled.Id);
        Assert.Equal(OmnichannelConstants.Events.SmsReceived, handled.EventType);
        Assert.Equal(OmnichannelConstants.Channels.Sms, handled.Message.Channel);
        Assert.Equal("+15550001111", handled.Message.CustomerAddress);
        Assert.Equal("+18880002222", handled.Message.ServiceAddress);
        Assert.Equal("Is my appointment still on?", handled.Message.Content);
        Assert.Equal("msg-1", handled.Message.ProviderMessageId);
        Assert.True(handled.Message.IsInbound);
        Assert.Equal(receivedUtc, handled.Message.CreatedUtc);
    }

    // A provider timestamp ahead of this server's clock would let the automated reply sort before the text it answers,
    // so it falls back to the server's clock.
    [Fact]
    public async Task HandleAsync_SmsReceivedWithAFutureTimestamp_StampsTheMessageWithTheClock()
    {
        // Arrange
        using var harness = new Harness();
        var context = Harness.CreateRequest(SmsReceivedEvent("msg-1", _nowUtc.AddHours(1)));

        // Act
        await harness.HandleAsync(context);
        var handled = await harness.WaitForHandledAsync();

        // Assert
        Assert.Equal(_nowUtc, handled.Message.CreatedUtc);
    }

    // Event Grid delivers at least once. Bug: a redelivered text was stored and answered again.
    [Fact]
    public async Task ProcessInboundSmsAsync_TheSameMessageIdDeliveredTwice_IsProcessedOnce()
    {
        // Arrange
        using var harness = new Harness();

        // Act
        await harness.ProcessInboundSmsAsync("msg-1");
        await harness.ProcessInboundSmsAsync("msg-1");

        // Assert
        Assert.Single(harness.Handled);
        harness.Session.Verify(
            session => session.SaveAsync(It.IsAny<object>(), It.IsAny<bool>(), OmnichannelConstants.CollectionName, It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.Contains(harness.Logger.At(LogLevel.Information), message => message.Contains("Ignoring a duplicate Event Grid SMS delivery"));
    }

    // Bug: a concurrency conflict while storing an inbound text lost it, because Event Grid had already been answered
    // and never redelivers. The text is retried in a fresh scope, and the retry must not mistake its own claim on the
    // message id for a duplicate delivery.
    [Fact]
    public async Task ProcessInboundSmsAsync_AConcurrencyConflictOnTheFirstTwoAttempts_IsRetriedAndProcessedOnce()
    {
        // Arrange
        using var harness = new Harness(saveFailure: attempt => attempt <= 2 ? new ConcurrencyException(new Document()) : null);

        // Act
        await harness.ProcessInboundSmsAsync("msg-1");

        // Assert
        Assert.Single(harness.Handled);
        Assert.Equal(3, harness.SaveAttempts);
        Assert.Equal(2, harness.Logger.At(LogLevel.Warning).Count(message => message.Contains("after a concurrency conflict")));
        Assert.Empty(harness.Logger.At(LogLevel.Error));
    }

    [Fact]
    public async Task ProcessInboundSmsAsync_AConcurrencyConflictOnEveryAttempt_GivesUpAfterThreeAndLogsAnError()
    {
        // Arrange
        using var harness = new Harness(saveFailure: _ => new ConcurrencyException(new Document()));

        // Act
        await harness.ProcessInboundSmsAsync("msg-1");

        // Assert
        Assert.Empty(harness.Handled);
        Assert.Equal(3, harness.SaveAttempts);
        Assert.Equal(2, harness.Logger.At(LogLevel.Warning).Count());
        Assert.Single(harness.Logger.At(LogLevel.Error), message => message.Contains("Failed to process the inbound Event Grid SMS"));
    }

    // Only a concurrency conflict is worth retrying; any other failure is recorded once so the lost text is visible.
    [Fact]
    public async Task ProcessInboundSmsAsync_ANonConcurrencyFailure_IsNotRetriedAndLogsAnError()
    {
        // Arrange
        using var harness = new Harness(saveFailure: _ => new InvalidOperationException("database is gone"));

        // Act
        await harness.ProcessInboundSmsAsync("msg-1");

        // Assert
        Assert.Empty(harness.Handled);
        Assert.Equal(1, harness.SaveAttempts);
        Assert.Empty(harness.Logger.At(LogLevel.Warning));
        Assert.Single(harness.Logger.At(LogLevel.Error));
    }

    private static string ValidationEvent(string validationCode)
        => JsonSerializer.Serialize(new[]
        {
            new
            {
                id = "evt-validation",
                topic = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Communication/communicationServices/test",
                subject = string.Empty,
                eventType = EventGridEventTypes.SubscriptionValidation,
                eventTime = "2026-09-28T12:00:00Z",
                dataVersion = "1",
                metadataVersion = "1",
                data = new
                {
                    validationCode,
                    validationUrl = "https://example.invalid/validate",
                },
            },
        });

    private static string SmsReceivedEvent(string messageId, DateTime receivedUtc)
        => JsonSerializer.Serialize(new[]
        {
            new
            {
                id = "evt-" + messageId,
                topic = "/subscriptions/test/resourceGroups/test/providers/Microsoft.Communication/communicationServices/test",
                subject = "/phonenumber/15550001111",
                eventType = EventGridEventTypes.AcsSmsReceived,
                eventTime = "2026-09-28T12:00:00Z",
                dataVersion = "1",
                metadataVersion = "1",
                data = new
                {
                    messageId,
                    from = "+15550001111",
                    to = "+18880002222",
                    message = "Is my appointment still on?",
                    receivedTimestamp = receivedUtc.ToString("O"),
                },
            },
        });

    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _services;
        private readonly ShellContext _shellContext;
        private readonly ShellSettings _shellSettings = new() { Name = "TenantA" };
        private readonly Mock<IShellHost> _shellHost = new();
        private readonly TaskCompletionSource<OmnichannelEvent> _firstHandled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Func<int, Exception> _saveFailure;
        private int _saveAttempts;

        public Harness(Func<int, Exception> saveFailure = null)
        {
            _saveFailure = saveFailure;

            Session
                .Setup(session => session.SaveAsync(It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    var attempt = Interlocked.Increment(ref _saveAttempts);
                    var failure = _saveFailure?.Invoke(attempt);

                    return failure is null ? Task.CompletedTask : Task.FromException(failure);
                });

            // The gate ships with Omnichannel management as a tenant singleton; the scoped logger is separate from the
            // endpoint's own logger, whose entries the tests count.
            _services = new ServiceCollection()
                .AddSingleton(Session.Object)
                .AddSingleton<IOmnichannelEventHandler>(new RecordingHandler(this))
                .AddSingleton<IAutomatedConversationGate, InMemoryAutomatedConversationGate>()
                .AddSingleton<ILogger<Startup>>(NullLogger<Startup>.Instance)
                .BuildServiceProvider();

            _shellContext = new ShellContext
            {
                Settings = _shellSettings,
                ServiceProvider = _services,
                IsActivated = true,
            };

            _shellHost
                .Setup(host => host.GetScopeAsync(It.IsAny<ShellSettings>()))
                .ReturnsAsync(() => new ShellScope(_shellContext));
        }

        public Mock<YesSqlSession> Session { get; } = new();

        public RecordingLogger<Startup> Logger { get; } = new();

        public List<OmnichannelEvent> Handled { get; } = [];

        public int SaveAttempts => Volatile.Read(ref _saveAttempts);

        public static DefaultHttpContext CreateRequest(string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            };

            context.Request.Method = HttpMethods.Post;
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
            context.Request.Body = new MemoryStream(bytes);
            context.Request.Headers["aeg-sas-key"] = SasKey;
            context.Response.Body = new MemoryStream();

            return context;
        }

        public Task<IResult> HandleAsync(HttpContext context)
        {
            var method = _endpointType.GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Static);

            return (Task<IResult>)method.Invoke(null,
            [
                context,
                Array.Empty<IOmnichannelEventHandler>(),
                Session.Object,
                new StubClock(_nowUtc),
                Options.Create(new EventGridOptions { EventGridSasKey = SasKey }),
                _shellHost.Object,
                _shellSettings,
                Logger,
            ]);
        }

        public Task ProcessInboundSmsAsync(string providerMessageId)
        {
            var method = _endpointType.GetMethod("ProcessInboundSmsAsync", BindingFlags.NonPublic | BindingFlags.Static);

            Func<OmnichannelEvent> createEvent = () => new OmnichannelEvent
            {
                Id = providerMessageId,
                EventType = OmnichannelConstants.Events.SmsReceived,
                Message = new OmnichannelMessage
                {
                    Channel = OmnichannelConstants.Channels.Sms,
                    CustomerAddress = "+15550001111",
                    ServiceAddress = "+18880002222",
                    Content = "hi",
                    IsInbound = true,
                    ProviderMessageId = providerMessageId,
                },
            };

            return (Task)method.Invoke(null,
            [
                _shellHost.Object,
                _shellSettings,
                createEvent,
                OmnichannelConstants.Channels.Sms,
                providerMessageId,
                Logger,
            ]);
        }

        public async Task<OmnichannelEvent> WaitForHandledAsync()
            => await _firstHandled.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        public void Dispose()
            => _services.Dispose();

        private sealed class RecordingHandler : IOmnichannelEventHandler
        {
            private readonly Harness _harness;

            public RecordingHandler(Harness harness)
            {
                _harness = harness;
            }

            public Task HandleAsync(OmnichannelEvent omnichannelEvent, CancellationToken cancellationToken = default)
            {
                lock (_harness.Handled)
                {
                    _harness.Handled.Add(omnichannelEvent);
                }

                _harness._firstHandled.TrySetResult(omnichannelEvent);

                return Task.CompletedTask;
            }
        }
    }
}
