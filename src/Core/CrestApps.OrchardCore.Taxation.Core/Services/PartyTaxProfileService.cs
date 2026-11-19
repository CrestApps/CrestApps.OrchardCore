using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// The default <see cref="IPartyTaxProfileService"/>: it starts from a person with no tax number and no exemption, and
/// lets every <see cref="IPartyTaxProfileProvider"/> add what it knows, in registration order.
/// </summary>
public sealed class PartyTaxProfileService : IPartyTaxProfileService
{
    private readonly IEnumerable<IPartyTaxProfileProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyTaxProfileService"/> class.
    /// </summary>
    /// <param name="providers">The providers that add what they know about a party.</param>
    public PartyTaxProfileService(IEnumerable<IPartyTaxProfileProvider> providers)
    {
        _providers = providers ?? [];
    }

    /// <inheritdoc/>
    public async Task<CustomerTaxProfile> GetAsync(PartyRef party, CancellationToken cancellationToken = default)
    {
        var profile = new CustomerTaxProfile();

        if (party.IsEmpty)
        {
            return profile;
        }

        foreach (var provider in _providers)
        {
            await provider.BuildAsync(profile, party, cancellationToken);
        }

        profile.ExemptionCertificateIds ??= [];

        return profile;
    }
}
