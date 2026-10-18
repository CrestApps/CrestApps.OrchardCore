using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Controllers;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A one-page checkout shows payment once every step before it that collects something is complete, and points at
/// the first one that isn't.
/// </summary>
public sealed class CheckoutOnePageTests
{
    [Fact]
    public void PaymentWaitsForTheFirstUnsavedStep()
    {
        var flow = Flow();

        Assert.False(CheckoutController.IsReadyToPay(flow));
        Assert.Equal("Contact", CheckoutController.FirstUnsavedStep(flow)?.Key);

        flow.Session.SavedSteps["Contact"] = new JsonObject();

        Assert.Equal("Shipping", CheckoutController.FirstUnsavedStep(flow)?.Key);
    }

    [Fact]
    public void StepsThatCollectNothingOrAreConcealedDoNotHoldPaymentBack()
    {
        var flow = Flow();
        flow.Session.SavedSteps["Contact"] = new JsonObject();
        flow.Session.SavedSteps["Shipping"] = new JsonObject();

        Assert.True(CheckoutController.IsReadyToPay(flow));
        Assert.Null(CheckoutController.FirstUnsavedStep(flow));
    }

    [Fact]
    public void PaymentItselfIsNeverAStepToFillFirst()
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            Steps =
            [
                new CheckoutFlowStep { Key = CheckoutConstants.PaymentStepKey, Order = 100, CollectData = true },
            ],
        };

        Assert.True(CheckoutController.IsReadyToPay(new CheckoutFlow(session)));
    }

    private static CheckoutFlow Flow()
        => new(new CheckoutSession
        {
            SessionId = "session-1",
            Steps =
            [
                new CheckoutFlowStep { Key = "Review", Order = 0, CollectData = false },
                new CheckoutFlowStep { Key = "Contact", Order = 1, CollectData = true },
                new CheckoutFlowStep { Key = "Shipping", Order = 2, CollectData = true },
                new CheckoutFlowStep { Key = "Account", Order = 3, CollectData = true, Conceal = true },
                new CheckoutFlowStep { Key = CheckoutConstants.PaymentStepKey, Order = 100, CollectData = false },
            ],
        });
}
