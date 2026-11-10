using CrestApps.OrchardCore.Stripe.Services;
using Stripe;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Stripe;

/// <summary>
/// A payment is charged at most once only if a retry after an unknown outcome reuses the idempotency key, and a retry
/// after a final refusal does not. These pin which of Stripe's errors are final.
/// </summary>
public sealed class StripeErrorFinalityTests
{
    [Theory]
    [InlineData("card_error", true)]
    [InlineData("invalid_request_error", true)]
    [InlineData("api_error", false)]
    [InlineData("idempotency_error", false)]
    [InlineData("rate_limit_error", false)]
    [InlineData(null, false)]
    public void IsFinal_OnlyForAnswersStripeGaveBeforeOrInsteadOfCharging(string type, bool expected)
    {
        Assert.Equal(expected, StripePaymentIntentService.IsFinal(new StripeError { Type = type }));
    }

    [Fact]
    public void IsFinal_WithNoError_IsUnknown()
    {
        Assert.False(StripePaymentIntentService.IsFinal(null));
    }
}
