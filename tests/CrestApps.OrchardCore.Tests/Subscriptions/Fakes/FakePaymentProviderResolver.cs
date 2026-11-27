using CrestApps.OrchardCore.Checkout.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Fakes;

/// <summary>
/// An <see cref="ICheckoutPaymentProviderResolver"/> with one provider per key: the keys passed defer payment, as Pay Later
/// and a bank transfer do, and any other key resolves to a gateway that collects at once.
/// </summary>
internal sealed class FakePaymentProviderResolver : ICheckoutPaymentProviderResolver
{
    private readonly HashSet<string> _deferring;

    public FakePaymentProviderResolver(params string[] deferringKeys)
    {
        _deferring = new HashSet<string>(deferringKeys, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<ICheckoutPaymentProvider> GetProviders()
        => [.. _deferring.Select(GetProvider)];

    public ICheckoutPaymentProvider GetProvider(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.Setup(candidate => candidate.Key).Returns(key);
        provider.Setup(candidate => candidate.Capabilities).Returns(new PaymentProviderCapabilities { DefersPayment = _deferring.Contains(key) });

        return provider.Object;
    }
}
