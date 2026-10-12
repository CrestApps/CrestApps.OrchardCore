using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using OrchardCore.Entities;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Starting a checkout builds its steps and their charges from the session, so anything that decides what
/// is being bought has to be on the session before that happens.
/// </summary>
/// <remarks>
/// Seeding it afterwards is silent and expensive: buying a plan on its annual price charged the monthly
/// one, the invoice said "monthly", and the checkout completed happily — the buyer simply got the wrong
/// thing at the wrong price. Only buying each price against a real gateway showed it.
/// </remarks>
public sealed class CheckoutStartSeedingTests
{
    /// <summary>
    /// The handler that builds the flow can see the buyer's chosen price.
    /// </summary>
    [Fact]
    public async Task StartAsync_SeedsThePriceSelection_BeforeTheFlowIsBuilt()
    {
        var recorder = new SelectionRecordingHandler();
        var store = new InMemoryCheckoutSessionStore([recorder]);

        await store.NewAsync(
            "Subscription",
            "plan-1",
            "version-1",
            session => session.Put(new CheckoutPriceSelection { PriceId = "annual", Quantity = 2, CustomAmount = 42m }),
            TestContext.Current.CancellationToken);

        Assert.NotNull(recorder.Seen);
        Assert.Equal("annual", recorder.Seen.PriceId);
        Assert.Equal(2, recorder.Seen.Quantity);
        Assert.Equal(42m, recorder.Seen.CustomAmount);
    }

    /// <summary>
    /// A checkout started without a choice still builds, and the handler simply sees none.
    /// </summary>
    [Fact]
    public async Task StartAsync_WithoutASelection_BuildsTheFlowAnyway()
    {
        var recorder = new SelectionRecordingHandler();
        var store = new InMemoryCheckoutSessionStore([recorder]);

        var session = await store.NewAsync("Subscription", "plan-1", "version-1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(recorder.Seen);
        Assert.NotNull(session);
    }

    private sealed class SelectionRecordingHandler : CheckoutHandlerBase
    {
        public CheckoutPriceSelection Seen { get; private set; }

        public override Task ActivatingAsync(CheckoutFlowActivatingContext context)
        {
            if (context.Session.TryGet<CheckoutPriceSelection>(out var selection))
            {
                Seen = selection;
            }

            return Task.CompletedTask;
        }
    }
}
