using System.Threading;
using System.Threading.Tasks;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;

namespace CrestApps.OrchardCore.Tests.Subscriptions.Fakes;

/// <summary>
/// A test <see cref="ISubscriptionTaxProfileProvider"/> that returns a fixed profile, letting tests
/// drive the destination, origin, customer, classification, and price type deterministically.
/// </summary>
public sealed class FixedSubscriptionTaxProfileProvider : ISubscriptionTaxProfileProvider
{
    private readonly SubscriptionTaxProfile _profile;

    public FixedSubscriptionTaxProfileProvider(SubscriptionTaxProfile profile)
    {
        _profile = profile;
    }

    public Task<SubscriptionTaxProfile> GetProfileAsync(SubscriptionFlow flow, CancellationToken cancellationToken = default)
        => Task.FromResult(_profile);

    public Task<SubscriptionTaxProfile> GetProfileAsync(ISubscriptionFlowSession session, CancellationToken cancellationToken = default)
        => Task.FromResult(_profile);
}
