using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Provides a configurable <see cref="IProviderIdentityProvider"/> for tests.
/// </summary>
internal sealed class TestProviderIdentityProvider : IProviderIdentityProvider
{
    private readonly ProviderIdentity[] _identities;

    public TestProviderIdentityProvider(params ProviderIdentity[] identities)
    {
        _identities = identities ?? [];
    }

    public IEnumerable<ProviderIdentity> GetIdentities()
        => _identities;
}
