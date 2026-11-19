using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Reads how to bill a party of any kind through the registered <see cref="IPartyBillingProfileSource"/>s, so an invoice
/// or a statement can be addressed to an account, a customer, a user or a contact without a checkout.
/// </summary>
public interface IPartyBillingProfileService
{
    /// <summary>
    /// Builds the party's billing profile: every source of its kind adds what it knows, in registration order. When no
    /// source names the party, the name comes from the <see cref="IPartyDirectory"/>.
    /// </summary>
    /// <param name="party">The party.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    /// <returns>The profile, never <see langword="null"/> for a party with a kind and an id, even when nothing is known
    /// about it; <see langword="null"/> for an empty reference.</returns>
    Task<PartyBillingProfile> GetAsync(PartyRef party, CancellationToken cancellationToken = default);
}
