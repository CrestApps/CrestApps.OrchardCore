using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Locking.Distributed;
using Xunit;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A checkout can pay for several things of one reference type at once. The engine checks the references are
/// consistent, and the handler of the reference type decides whether they can be paid together.
/// </summary>
public sealed class CheckoutReferencesTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task StartAsync_WithSeveralReferences_KeepsThemOnTheSession()
    {
        // Arrange
        var (engine, _) = CreateEngine();

        // Act
        var session = await engine.StartAsync(Request(("Fee", "fee-1"), ("Fee", "fee-2")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Fee", session.ReferenceType);
        Assert.Null(session.ReferenceId);
        Assert.Equal(["fee-1", "fee-2"], session.References.Select(reference => reference.ReferenceId));
    }

    [Fact]
    public async Task StartAsync_WithASingleReference_IsUnchanged()
    {
        // Arrange
        var (engine, _) = CreateEngine();

        // Act
        var session = await engine.StartAsync(new StartCheckoutRequest { ReferenceType = "Fee", ReferenceId = "fee-1", ReferenceVersionId = "v1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Fee", session.ReferenceType);
        Assert.Equal("fee-1", session.ReferenceId);
        Assert.Equal("v1", session.ReferenceVersionId);
        Assert.Empty(session.References);
    }

    [Fact]
    public async Task StartAsync_WithReferencesOfDifferentTypes_IsRefused()
    {
        // Arrange
        var (engine, sessions) = CreateEngine();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => engine.StartAsync(Request(("Fee", "fee-1"), ("Order", "order-1")), TestContext.Current.CancellationToken));
        Assert.Equal(0, sessions.SaveCount);
    }

    [Fact]
    public async Task StartAsync_WithTheSameReferenceTwice_IsRefused()
    {
        // Arrange
        var (engine, _) = CreateEngine();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => engine.StartAsync(Request(("Fee", "fee-1"), ("Fee", "fee-1")), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_WithAReferenceIdAndReferences_IsRefused()
    {
        // Arrange
        var (engine, _) = CreateEngine();
        var request = Request(("Fee", "fee-1"), ("Fee", "fee-2"));
        request.ReferenceId = "fee-3";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => engine.StartAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_WhenAHandlerRejectsTheCheckout_ThrowsAndSavesNothing()
    {
        // Arrange
        var (engine, sessions) = CreateEngine(new RejectingHandler());

        // Act
        var exception = await Assert.ThrowsAsync<CheckoutRejectedException>(() => engine.StartAsync(Request(("Fee", "fee-1"), ("Fee", "fee-2")), TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(["These can't be paid together."], exception.Errors);
        Assert.Equal(0, sessions.SaveCount);
    }

    private static StartCheckoutRequest Request(params (string Type, string Id)[] references)
        => new()
        {
            References = [.. references.Select(reference => new CheckoutReference { ReferenceType = reference.Type, ReferenceId = reference.Id })],
        };

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore Sessions) CreateEngine(params ICheckoutHandler[] handlers)
    {
        var sessions = new InMemoryCheckoutSessionStore(handlers);
        var attemptStore = new InMemoryPaymentAttemptStore();
        var providerResolver = Mock.Of<ICheckoutPaymentProviderResolver>();

        var engine = new DefaultCheckoutEngine(
            sessions,
            attemptStore,
            providerResolver,
            Mock.Of<ICheckoutRecurringPaymentProviderResolver>(),
            new CheckoutReconciliationService(attemptStore, providerResolver, NullLogger<CheckoutReconciliationService>.Instance),
            Mock.Of<ICheckoutRefundService>(),
            [],
            Mock.Of<IDistributedLock>(),
            Mock.Of<ISession>(),
            new TestClock(_now),
            NullLogger<DefaultCheckoutEngine>.Instance);

        return (engine, sessions);
    }

    private sealed class RejectingHandler : CheckoutHandlerBase
    {
        public override Task ActivatingAsync(CheckoutFlowActivatingContext context)
        {
            if (context.Session.References.Count > 0)
            {
                context.Reject("These can't be paid together.");
            }

            return Task.CompletedTask;
        }
    }
}
