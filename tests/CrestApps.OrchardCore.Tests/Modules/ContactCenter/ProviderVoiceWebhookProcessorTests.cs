using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceWebhookProcessorTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
    private readonly ProviderWebhookIngressLimiter _ingressLimiter = CreateIngressLimiter();

    [Fact]
    public async Task ProcessAsync_WithValidSignatureAndKeyedEvents_IngestsEachEvent()
    {
        // Arrange
        var inbox = CreateInbox();
        var adapter = new FakeAdapter
        {
            Parser = _ =>
            [
                new ProviderVoiceEvent { ProviderCallId = "c1", IdempotencyKey = "k1", OccurredUtc = _now },
                new ProviderVoiceEvent { ProviderCallId = "c1", IdempotencyKey = "k2", OccurredUtc = _now },
            ],
        };

        var processor = CreateProcessor(inbox, adapter);

        // Act
        var outcome = await processor.ProcessAsync(new ProviderVoiceWebhookRequest { Provider = "fake" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.Accepted, outcome.Status);
        Assert.Equal(2, outcome.ProcessedCount);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        inbox.Verify(
            service => service.DispatchAsync(It.IsAny<string>(), It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessAsync_WithUnknownProvider_ReturnsUnknownProvider()
    {
        // Arrange
        var inbox = CreateInbox();
        var processor = CreateProcessor(inbox, new FakeAdapter { TechnicalName = "other" });

        // Act
        var outcome = await processor.ProcessAsync(new ProviderVoiceWebhookRequest { Provider = "fake" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.UnknownProvider, outcome.Status);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidSignature_RejectsWithoutIngesting()
    {
        // Arrange
        var inbox = CreateInbox();
        var adapter = new FakeAdapter { Validate = _ => false };
        var processor = CreateProcessor(inbox, adapter);

        // Act
        var outcome = await processor.ProcessAsync(new ProviderVoiceWebhookRequest { Provider = "fake" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.InvalidSignature, outcome.Status);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithEventMissingIdempotencyKey_RejectsWithoutIngesting()
    {
        // Arrange
        var inbox = CreateInbox();
        var adapter = new FakeAdapter
        {
            Parser = _ => [new ProviderVoiceEvent { ProviderCallId = "c1" }],
        };

        var processor = CreateProcessor(inbox, adapter);

        // Act
        var outcome = await processor.ProcessAsync(new ProviderVoiceWebhookRequest { Provider = "fake" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.MissingIdempotencyKey, outcome.Status);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithOversizedIdempotencyKey_RejectsWithoutAccepting()
    {
        // Arrange
        var inbox = CreateInbox();
        var adapter = new FakeAdapter
        {
            Parser = _ =>
            [
                new ProviderVoiceEvent
                {
                    ProviderCallId = "c1",
                    IdempotencyKey = new string('x', ProviderWebhookInbox.MaxDeliveryIdLength + 1),
                    OccurredUtc = _now,
                },
            ],
        };
        var processor = CreateProcessor(inbox, adapter);

        // Act
        var outcome = await processor.ProcessAsync(
            new ProviderVoiceWebhookRequest { Provider = "fake" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.MissingIdempotencyKey, outcome.Status);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void HmacAdapter_ValidatesMatchingSignatureAndRejectsTampering()
    {
        // Arrange
        var adapter = new TestHmacAdapter("shhh");
        const string body = "{\"call\":\"c1\"}";
        var validRequest = new ProviderVoiceWebhookRequest { Body = body };
        validRequest.Headers["X-Signature"] = adapter.Sign(body);

        var tamperedRequest = new ProviderVoiceWebhookRequest { Body = body + "x" };
        tamperedRequest.Headers["X-Signature"] = adapter.Sign(body);

        // Act & Assert
        Assert.True(adapter.ValidateSignature(validRequest));
        Assert.False(adapter.ValidateSignature(tamperedRequest));
    }

    [Fact]
    public void HmacAdapter_WithoutConfiguredSecret_RejectsSignature()
    {
        // Arrange
        var adapter = new TestHmacAdapter(secret: null);
        var request = new ProviderVoiceWebhookRequest { Body = "{}" };
        request.Headers["X-Signature"] = "deadbeef";

        // Act
        var valid = adapter.ValidateSignature(request);

        // Assert
        Assert.False(valid);
    }

    [Fact]
    public async Task ProcessAsync_WhenAuthenticatedProviderExceedsRateLimit_ReturnsRateLimited()
    {
        // Arrange
        using var limiter = CreateIngressLimiter(ratePermitLimit: 1);
        var inbox = CreateInbox();
        var adapter = new FakeAdapter();
        var processor = new ProviderVoiceWebhookProcessor(
            [adapter],
            inbox.Object,
            limiter,
            NullLogger<ProviderVoiceWebhookProcessor>.Instance);
        var request = new ProviderVoiceWebhookRequest { Provider = adapter.TechnicalName };
        await processor.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Act
        var outcome = await processor.ProcessAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.RateLimited, outcome.Status);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenDurableInboxIsBusy_ReturnsInboxBusyWithoutDispatching()
    {
        // Arrange
        var inbox = CreateInbox();
        inbox
            .Setup(service => service.AcceptAsync(
                It.IsAny<ProviderWebhookInboxDelivery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Busy,
            });
        var processor = CreateProcessor(inbox, new FakeAdapter
        {
            Parser = _ =>
            [
                new ProviderVoiceEvent
                {
                    ProviderCallId = "c1",
                    IdempotencyKey = "k1",
                    OccurredUtc = _now,
                },
            ],
        });

        // Act
        var outcome = await processor.ProcessAsync(
            new ProviderVoiceWebhookRequest { Provider = "fake" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.InboxBusy, outcome.Status);
        inbox.Verify(
            service => service.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WhenDeliveryIsDuplicate_ReturnsAcceptedWithoutCountingANewMessage()
    {
        // Arrange
        var inbox = CreateInbox();
        inbox
            .Setup(service => service.AcceptAsync(
                It.IsAny<ProviderWebhookInboxDelivery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Duplicate,
                MessageId = "message-1",
            });
        var processor = CreateProcessor(inbox, new FakeAdapter
        {
            Parser = _ =>
            [
                new ProviderVoiceEvent
                {
                    ProviderCallId = "c1",
                    IdempotencyKey = "k1",
                    OccurredUtc = _now,
                },
            ],
        });

        // Act
        var outcome = await processor.ProcessAsync(
            new ProviderVoiceWebhookRequest { Provider = "fake" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.Accepted, outcome.Status);
        Assert.Equal(0, outcome.ProcessedCount);
        inbox.Verify(
            service => service.DispatchAsync(
                "message-1",
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
    }

    [Theory]
    [InlineData(-901)]
    [InlineData(121)]
    public async Task ProcessAsync_WhenSignedEventIsOutsideFreshnessWindow_RejectsWithoutIngesting(int offsetSeconds)
    {
        // Arrange
        var inbox = CreateInbox();
        var adapter = new FakeAdapter
        {
            Parser = _ =>
            [
                new ProviderVoiceEvent
                {
                    ProviderCallId = "c1",
                    IdempotencyKey = "k1",
                    OccurredUtc = _now.AddSeconds(offsetSeconds),
                },
            ],
        };
        var processor = CreateProcessor(inbox, adapter);

        // Act
        var outcome = await processor.ProcessAsync(
            new ProviderVoiceWebhookRequest { Provider = adapter.TechnicalName },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.StaleDelivery, outcome.Status);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public async Task ProcessAsync_WhenSignedEventTimestampIsMissingOrNotUtc_RejectsWithoutIngesting(DateTimeKind? kind)
    {
        // Arrange
        var inbox = CreateInbox();
        var occurredUtc = kind.HasValue
            ? DateTime.SpecifyKind(_now, kind.Value)
            : (DateTime?)null;
        var adapter = new FakeAdapter
        {
            Parser = _ =>
            [
                new ProviderVoiceEvent
                {
                    ProviderCallId = "c1",
                    IdempotencyKey = "k1",
                    OccurredUtc = occurredUtc,
                },
            ],
        };
        var processor = CreateProcessor(inbox, adapter);

        // Act
        var outcome = await processor.ProcessAsync(
            new ProviderVoiceWebhookRequest { Provider = adapter.TechnicalName },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ProviderVoiceWebhookStatus.StaleDelivery, outcome.Status);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _ingressLimiter.Dispose();
    }

    private ProviderVoiceWebhookProcessor CreateProcessor(
        Mock<IProviderWebhookInbox> inbox,
        params IProviderVoiceWebhookAdapter[] adapters)
    {
        return new ProviderVoiceWebhookProcessor(
            adapters,
            inbox.Object,
            _ingressLimiter,
            NullLogger<ProviderVoiceWebhookProcessor>.Instance);
    }

    private static Mock<IProviderWebhookInbox> CreateInbox()
    {
        var inbox = new Mock<IProviderWebhookInbox>();
        var messageNumber = 0;
        inbox
            .Setup(service => service.AcceptAsync(
                It.IsAny<ProviderWebhookInboxDelivery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Accepted,
                MessageId = $"message-{Interlocked.Increment(ref messageNumber)}",
            });
        inbox
            .Setup(service => service.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return inbox;
    }

    private static ProviderWebhookIngressLimiter CreateIngressLimiter(int ratePermitLimit = 120)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ProviderWebhookIngressLimiter(
            Options.Create(new ProviderWebhookIngressOptions
            {
                RatePermitLimit = ratePermitLimit,
            }),
            clock.Object);
    }

    private sealed class FakeAdapter : IProviderVoiceWebhookAdapter
    {
        public string TechnicalName { get; init; } = "fake";

        public Func<ProviderVoiceWebhookRequest, bool> Validate { get; init; } = _ => true;

        public Func<ProviderVoiceWebhookRequest, IReadOnlyList<ProviderVoiceEvent>> Parser { get; init; } = _ => [];

        public bool ValidateSignature(ProviderVoiceWebhookRequest request) => Validate(request);

        public IReadOnlyList<ProviderVoiceEvent> Parse(ProviderVoiceWebhookRequest request) => Parser(request);
    }

    private sealed class TestHmacAdapter : HmacProviderVoiceWebhookAdapterBase
    {
        public TestHmacAdapter(string secret)
        {
            SigningSecret = secret;
        }

        public override string TechnicalName => "test";

        protected override string SignatureHeaderName => "X-Signature";

        protected override string SigningSecret { get; }

        public override IReadOnlyList<ProviderVoiceEvent> Parse(ProviderVoiceWebhookRequest request) => [];

        public string Sign(string body) => ComputeSignature(SigningSecret, body);
    }
}
