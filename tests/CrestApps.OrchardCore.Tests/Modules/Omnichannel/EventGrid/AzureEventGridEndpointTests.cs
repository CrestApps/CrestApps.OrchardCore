using System.Reflection;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
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
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using Startup = CrestApps.OrchardCore.Omnichannel.EventGrid.Startup;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.EventGrid;

/// <summary>
/// The Azure Event Grid webhook. The endpoint is an internal minimal-API handler, so these tests reach its handler by
/// reflection. Each test names the bug it guards.
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
    public async Task HandleAsync_SmsReceivedWithoutAnSmsFeature_RaisesTheSmsReceivedEventWithTheProviderReceiveTime()
    {
        // Arrange
        using var harness = new Harness();
        var receivedUtc = _nowUtc.AddMinutes(-3);
        var context = Harness.CreateRequest(SmsReceivedEvent("msg-1", receivedUtc));

        // Act
        var result = await harness.HandleAsync(context);

        // Assert
        Assert.IsType<Ok>(result);
        var handled = Assert.Single(harness.Handled);
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

        // Assert
        var handled = Assert.Single(harness.Handled);
        Assert.Equal(_nowUtc, handled.Message.CreatedUtc);
    }

    // Bug: an inbound text was processed in a discarded background task, so a restart lost it, and redeliveries were
    // recognised only by an in-memory claim that another node never saw. With an SMS feature on, the text is committed
    // to the durable provider webhook inbox, keyed on the provider's message id, before Event Grid is answered.
    [Fact]
    public async Task HandleAsync_SmsReceived_IsStoredInTheInboxBeforeEventGridIsAnswered()
    {
        // Arrange
        using var harness = new Harness();
        ProviderWebhookInboxDelivery accepted = null;
        harness.Inbox
            .Setup(inbox => inbox.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .Callback((ProviderWebhookInboxDelivery delivery, CancellationToken _) => accepted = delivery)
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Accepted,
                MessageId = "inbox-1",
            });

        var context = harness.CreateRequestWithInbox(SmsReceivedEvent("msg-1", _nowUtc.AddMinutes(-3)));

        // Act
        var result = await harness.HandleAsync(context);

        // Assert
        Assert.IsType<Ok>(result);
        Assert.NotNull(accepted);
        Assert.Equal("msg-1", accepted.DeliveryId);
        Assert.Equal(OmnichannelConstants.WebhookInboxHandlers.SmsReceived, accepted.HandlerName);

        var message = JsonSerializer.Deserialize<OmnichannelMessage>(accepted.Payload);
        Assert.Equal("msg-1", message.ProviderMessageId);
        Assert.Equal("Is my appointment still on?", message.Content);

        // The text is processed from the inbox, not inline and not by this request's session.
        Assert.Empty(harness.Handled);
        harness.Session.Verify(
            session => session.SaveAsync(It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Event Grid delivers at least once. Bug: a redelivered text was stored and answered again.
    [Fact]
    public async Task HandleAsync_ARedeliveredText_IsAcknowledgedAndNotProcessedAgain()
    {
        // Arrange
        using var harness = new Harness();
        harness.Inbox
            .Setup(inbox => inbox.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Duplicate,
                MessageId = "inbox-1",
            });

        var context = harness.CreateRequestWithInbox(SmsReceivedEvent("msg-1", _nowUtc.AddMinutes(-3)));

        // Act
        var result = await harness.HandleAsync(context);

        // Assert
        Assert.IsType<Ok>(result);
        Assert.Empty(harness.Handled);
        harness.Inbox.Verify(inbox => inbox.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(harness.Logger.At(LogLevel.Information), message => message.Contains("Ignoring a duplicate Event Grid SMS delivery"));
    }

    // Bug: a text that could not be stored was lost, because Event Grid had already been answered. Now it is not
    // answered with success, so Event Grid delivers it again.
    [Fact]
    public async Task HandleAsync_WhenTheInboxCannotStoreTheText_AsksEventGridToDeliverItAgain()
    {
        // Arrange
        using var harness = new Harness();
        harness.Inbox
            .Setup(inbox => inbox.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database is gone"));

        var context = harness.CreateRequestWithInbox(SmsReceivedEvent("msg-1", _nowUtc.AddMinutes(-3)));

        // Act
        var result = await harness.HandleAsync(context);

        // Assert
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
        Assert.Empty(harness.Handled);
        Assert.Single(harness.Logger.At(LogLevel.Error), message => message.Contains("could not be stored"));
    }

    [Fact]
    public async Task HandleAsync_WhenTheInboxIsBusy_AsksEventGridToDeliverItAgain()
    {
        // Arrange
        using var harness = new Harness();
        harness.Inbox
            .Setup(inbox => inbox.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Busy,
            });

        var context = harness.CreateRequestWithInbox(SmsReceivedEvent("msg-1", _nowUtc.AddMinutes(-3)));

        // Act
        var result = await harness.HandleAsync(context);

        // Assert
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
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
        private readonly ShellSettings _shellSettings = new() { Name = "TenantA" };
        private readonly Mock<IShellHost> _shellHost = new();
        private readonly RecordingHandler _handler;
        private readonly List<ServiceProvider> _requestServices = [];

        public Harness()
        {
            _handler = new RecordingHandler(this);

            Session
                .Setup(session => session.SaveAsync(It.IsAny<object>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public Mock<YesSqlSession> Session { get; } = new();

        public Mock<IProviderWebhookInbox> Inbox { get; } = new();

        public RecordingLogger<Startup> Logger { get; } = new();

        public List<OmnichannelEvent> Handled { get; } = [];

        public static DefaultHttpContext CreateRequest(string body)
            => CreateRequest(body, new ServiceCollection().AddLogging().BuildServiceProvider());

        /// <summary>
        /// Creates a request on a tenant with an SMS feature on, which brings the provider webhook inbox and its
        /// inbound SMS handler.
        /// </summary>
        public DefaultHttpContext CreateRequestWithInbox(string body)
        {
            var handler = new Mock<IProviderWebhookInboxHandler>();
            handler.SetupGet(service => service.TechnicalName).Returns(OmnichannelConstants.WebhookInboxHandlers.SmsReceived);

            var services = new ServiceCollection()
                .AddLogging()
                .AddSingleton(Inbox.Object)
                .AddSingleton(handler.Object)
                .BuildServiceProvider();

            _requestServices.Add(services);

            return CreateRequest(body, services);
        }

        public Task<IResult> HandleAsync(HttpContext context)
        {
            var method = _endpointType.GetMethod("HandleAsync", BindingFlags.NonPublic | BindingFlags.Static);

            return (Task<IResult>)method.Invoke(null,
            [
                context,
                new IOmnichannelEventHandler[] { _handler },
                Session.Object,
                new StubClock(_nowUtc),
                Options.Create(new EventGridOptions { EventGridSasKey = SasKey }),
                _shellHost.Object,
                _shellSettings,
                Logger,
            ]);
        }

        public void Dispose()
        {
            foreach (var services in _requestServices)
            {
                services.Dispose();
            }
        }

        private static DefaultHttpContext CreateRequest(string body, IServiceProvider services)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var context = new DefaultHttpContext
            {
                RequestServices = services,
            };

            context.Request.Method = HttpMethods.Post;
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = bytes.Length;
            context.Request.Body = new MemoryStream(bytes);
            context.Request.Headers["aeg-sas-key"] = SasKey;
            context.Response.Body = new MemoryStream();

            return context;
        }

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

                return Task.CompletedTask;
            }
        }
    }
}
