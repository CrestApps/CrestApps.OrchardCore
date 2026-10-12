using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// A configurable fake <see cref="ICheckoutPaymentRefundProvider"/> that records the refund contexts it
/// receives and returns a supplied result, so the refund orchestration can be asserted without a gateway.
/// </summary>
internal sealed class FakeCheckoutPaymentRefundProvider : ICheckoutPaymentRefundProvider
{
    private readonly Func<RefundPaymentContext, PaymentRefundResult> _refund;

    public FakeCheckoutPaymentRefundProvider(string key, Func<RefundPaymentContext, PaymentRefundResult> refund)
    {
        Key = key;
        _refund = refund;
    }

    public string Key { get; }

    public List<RefundPaymentContext> Contexts { get; } = [];

    public Task<PaymentRefundResult> RefundAsync(RefundPaymentContext context, CancellationToken cancellationToken = default)
    {
        Contexts.Add(context);

        return Task.FromResult(_refund(context));
    }
}
