using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Api;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// How the checkout API reads a checkout, names its problems, and replays a payment asked for twice.
/// </summary>
public sealed class CommerceCheckoutApiTests
{
    [Fact]
    public void TheNextStep_IsTheFirstOneNotDone_ThenPayment()
    {
        var session = NewSession();

        Assert.Equal("Contact", CheckoutApi.Read(session).NextStep);

        session.SavedSteps["Contact"] = new System.Text.Json.Nodes.JsonObject();
        session.SavedSteps["Shipping"] = new System.Text.Json.Nodes.JsonObject();

        var checkout = CheckoutApi.Read(session);

        Assert.Equal(CheckoutApi.PaymentStep, checkout.NextStep);
        Assert.All(checkout.Steps, step => Assert.True(step.IsDone));
    }

    [Fact]
    public void AFinishedCheckout_HasNoNextStep()
    {
        var session = NewSession();
        session.Status = CheckoutSessionStatus.Completed;
        session.SavedSteps["Contact"] = new System.Text.Json.Nodes.JsonObject();
        session.SavedSteps["Shipping"] = new System.Text.Json.Nodes.JsonObject();

        var checkout = CheckoutApi.Read(session);

        Assert.Null(checkout.NextStep);
        Assert.Equal("completed", checkout.Status);
        Assert.Equal("order-1", checkout.OrderId);
    }

    [Fact]
    public void Shipping_IsTotaledOnItsOwn_NotInTheSubtotal()
    {
        var session = NewSession();
        session.Put(new CheckoutInvoice
        {
            Currency = "USD",
            GrandTotal = 79m,
            LineItems =
            [
                new CheckoutLineItem { ItemId = "line-1", Description = "Backpack", Quantity = 1, UnitPrice = 79m },
                new CheckoutLineItem { ItemId = "shipping", Description = "Standard", Quantity = 1, UnitPrice = 14m, DiscountAmount = 14m, TaxableKind = TaxableItemKind.Shipping },
            ],
            Discounts = [new() { Code = "FREESHIP75", Amount = 14m, Target = DiscountTarget.Shipping }],
        });

        var totals = CheckoutApi.Read(session).Totals;

        Assert.Equal(new ApiMoney(79m, "USD"), totals.Subtotal);
        Assert.Equal(new ApiMoney(14m, "USD"), totals.Shipping);
        Assert.Equal(new ApiMoney(14m, "USD"), totals.Discount);
        Assert.Equal(new ApiMoney(79m, "USD"), totals.Total);
        Assert.Equal("shipping", CheckoutApi.Read(session).Discounts.Single().Target);
    }

    [Fact]
    public void ProblemsAreKeyed_ByTheRequestsJsonNames()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Email", "Enter your email.");
        modelState.AddModelError("BillingAddress.City", "City is required.");
        modelState.AddModelError("Rates", string.Empty);

        var problem = ApiProblems.Validation(modelState);

        Assert.Equal(["Enter your email."], problem.ProblemDetails.Errors["email"]);
        Assert.Equal(["City is required."], problem.ProblemDetails.Errors["billingAddress.city"]);
        Assert.False(problem.ProblemDetails.Errors.ContainsKey("rates"));
    }

    [Fact]
    public void ATypedUnion_IsKeptByTheValueItWraps()
    {
        Results<Ok<ApiMoney>, ProblemHttpResult> ok = TypedResults.Ok(new ApiMoney(5m, "USD"));
        Results<Ok<ApiMoney>, ProblemHttpResult> conflict = ApiProblems.Conflict(ApiProblems.Codes.InvoiceChanged, "The total changed.");

        var stored = IdempotencyEndpointFilter.Capture(ok);
        var storedConflict = IdempotencyEndpointFilter.Capture(conflict);

        Assert.Equal(StatusCodes.Status200OK, stored.StatusCode);
        Assert.Contains("\"amount\":5", stored.Body, StringComparison.Ordinal);
        Assert.Equal(StatusCodes.Status409Conflict, storedConflict.StatusCode);
        Assert.Contains("invoice-changed", storedConflict.Body, StringComparison.Ordinal);
    }

    private static CheckoutSession NewSession()
    {
        var session = new CheckoutSession
        {
            SessionId = "checkout-1",
            ReferenceType = CheckoutReferenceTypes.Order,
            ReferenceId = "order-1",
            Currency = "USD",
            Status = CheckoutSessionStatus.Pending,
        };

        session.Steps.Add(new CheckoutFlowStep { Key = "Contact", Title = "Contact", Order = 1, CollectData = true });
        session.Steps.Add(new CheckoutFlowStep { Key = "Shipping", Title = "Shipping", Order = 2, CollectData = true });
        session.Steps.Add(new CheckoutFlowStep { Key = CheckoutConstants.PaymentStepKey, Title = "Payment", Order = 3 });

        return session;
    }
}
