using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Adds what a module knows about a party's tax status to its tax profile, such as whether a customer is a business,
/// or the exemption certificates on file for them. Several providers may contribute; each fills what it knows and keeps
/// what another one set, so the order they run in doesn't change the result. Used to tax a document billed to a party
/// without a checkout, such as an invoice billed to an account.
/// </summary>
public interface IPartyTaxProfileProvider
{
    /// <summary>
    /// Adds what the provider knows about the party to its tax profile.
    /// </summary>
    /// <param name="profile">The profile being built.</param>
    /// <param name="party">The party.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    Task BuildAsync(CustomerTaxProfile profile, PartyRef party, CancellationToken cancellationToken = default);
}
