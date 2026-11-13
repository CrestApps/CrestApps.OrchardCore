using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// Adds what a module knows about how to bill a party of one kind, such as an account's name and billing address, or the
/// tax number and terms a business buying module keeps for it. Several sources may contribute to one kind: they run in
/// registration order, and a later one fills or overrides what an earlier one set. Register one with
/// <see cref="PartiesServiceCollectionExtensions.AddPartyBillingProfileSource{TSource}"/>.
/// </summary>
public interface IPartyBillingProfileSource
{
    /// <summary>
    /// Gets the kind of party the source knows, one of <see cref="PartyKinds"/> or a kind a module adds.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Adds what the source knows about the party to its profile. A party the source doesn't find is left as it is.
    /// </summary>
    /// <param name="party">The party, of the source's kind.</param>
    /// <param name="profile">The profile being built, with what earlier sources set.</param>
    /// <param name="cancellationToken">A token to cancel the lookup.</param>
    Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default);
}
