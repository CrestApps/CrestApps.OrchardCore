using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A configurable fake <see cref="ICheckoutPaymentProvider"/> whose verification result is supplied per
/// test so orphan-prevention behavior can be asserted against every provider outcome.
/// </summary>
internal sealed class FakeCheckoutPaymentProvider : ICheckoutPaymentProvider
{
    private readonly Func<VerifyPaymentContext, PaymentVerificationResult> _verify;

    public FakeCheckoutPaymentProvider(string key, Func<VerifyPaymentContext, PaymentVerificationResult> verify)
    {
        Key = key;
        _verify = verify;
    }

    public string Key { get; }

    public string DisplayName => Key;

    public PaymentProviderCapabilities Capabilities { get; } = new()
    {
        SupportsOneTimePayments = true,
        SupportsRecurringPayments = true,
    };

    public int VerifyCallCount { get; private set; }

    public Task<PaymentBeginResult> BeginAsync(BeginPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentBeginResult.Success("ref"));

    public Task<PaymentVerificationResult> VerifyAsync(VerifyPaymentContext context, CancellationToken cancellationToken = default)
    {
        VerifyCallCount++;

        return Task.FromResult(_verify(context));
    }

    public Task<PaymentCancelResult> CancelAsync(CancelPaymentContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentCancelResult.Success());
}
