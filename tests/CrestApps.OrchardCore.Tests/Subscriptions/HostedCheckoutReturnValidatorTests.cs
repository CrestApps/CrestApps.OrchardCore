using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class HostedCheckoutReturnValidatorTests
{
    private static CheckoutSessionDetails PaidSession(string clientRef = "session-1", string currency = "usd")
        => new()
        {
            Id = "cs_1",
            Status = "complete",
            PaymentStatus = "paid",
            SubscriptionId = "sub_1",
            ClientReferenceId = clientRef,
            Currency = currency,
        };

    [Fact]
    public void Validate_ReturnsValid_WhenPaidAndBoundToSession()
    {
        var result = HostedCheckoutReturnValidator.Validate(PaidSession(), "session-1", "usd");

        Assert.Equal(CheckoutReturnValidation.Valid, result);
    }

    [Fact]
    public void Validate_ReturnsNotConfirmed_WhenDetailsNull()
    {
        var result = HostedCheckoutReturnValidator.Validate(null, "session-1", "usd");

        Assert.Equal(CheckoutReturnValidation.NotConfirmed, result);
    }

    [Fact]
    public void Validate_ReturnsNotConfirmed_WhenNotPaid()
    {
        var details = PaidSession();
        details.PaymentStatus = "unpaid";

        var result = HostedCheckoutReturnValidator.Validate(details, "session-1", "usd");

        Assert.Equal(CheckoutReturnValidation.NotConfirmed, result);
    }

    [Fact]
    public void Validate_ReturnsNotConfirmed_WhenNoSubscription()
    {
        var details = PaidSession();
        details.SubscriptionId = null;

        var result = HostedCheckoutReturnValidator.Validate(details, "session-1", "usd");

        Assert.Equal(CheckoutReturnValidation.NotConfirmed, result);
    }

    [Fact]
    public void Validate_ReturnsNotConfirmed_WhenClientReferenceDoesNotMatchSession()
    {
        // A valid, paid checkout for a DIFFERENT session must never finalize this session.
        var result = HostedCheckoutReturnValidator.Validate(PaidSession(clientRef: "other-session"), "session-1", "usd");

        Assert.Equal(CheckoutReturnValidation.NotConfirmed, result);
    }

    [Fact]
    public void Validate_ReturnsNotConfirmed_WhenLocalSessionIdMissing()
    {
        var result = HostedCheckoutReturnValidator.Validate(PaidSession(clientRef: null), null, "usd");

        Assert.Equal(CheckoutReturnValidation.NotConfirmed, result);
    }

    [Fact]
    public void Validate_ReturnsCurrencyMismatch_WhenCurrenciesDiffer()
    {
        var result = HostedCheckoutReturnValidator.Validate(PaidSession(currency: "eur"), "session-1", "usd");

        Assert.Equal(CheckoutReturnValidation.CurrencyMismatch, result);
    }

    [Fact]
    public void Validate_IgnoresCurrency_WhenInvoiceCurrencyUnknown()
    {
        var result = HostedCheckoutReturnValidator.Validate(PaidSession(currency: "eur"), "session-1", invoiceCurrency: null);

        Assert.Equal(CheckoutReturnValidation.Valid, result);
    }

    [Fact]
    public void Validate_CurrencyComparisonIsCaseInsensitive()
    {
        var result = HostedCheckoutReturnValidator.Validate(PaidSession(currency: "USD"), "session-1", "usd");

        Assert.Equal(CheckoutReturnValidation.Valid, result);
    }
}
