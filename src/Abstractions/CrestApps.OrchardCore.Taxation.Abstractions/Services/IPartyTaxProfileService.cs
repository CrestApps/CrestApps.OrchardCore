using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Builds the tax profile of a party of any kind through every <see cref="IPartyTaxProfileProvider"/>, so a document
/// billed to an account, a customer, a user or a contact gets the party's exemption, tax number and business status
/// without a checkout.
/// </summary>
public interface IPartyTaxProfileService
{
    /// <summary>
    /// Builds the party's tax profile. A party nothing is known about is taxed as a person, with no tax number and no
    /// exemption.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The profile, never <see langword="null"/>.</returns>
    Task<CustomerTaxProfile> GetAsync(PartyRef party, CancellationToken cancellationToken = default);
}
