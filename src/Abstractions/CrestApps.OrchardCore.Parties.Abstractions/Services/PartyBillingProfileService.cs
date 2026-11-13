using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Parties.Services;

/// <summary>
/// The default <see cref="IPartyBillingProfileService"/>: every source of the party's kind adds what it knows, in
/// registration order, and the <see cref="IPartyDirectory"/> names a party no source named. Kinds compare without case.
/// </summary>
public sealed class PartyBillingProfileService : IPartyBillingProfileService
{
    private readonly IEnumerable<IPartyBillingProfileSource> _sources;
    private readonly IPartyDirectory _directory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartyBillingProfileService"/> class.
    /// </summary>
    /// <param name="sources">The billing profile sources, in registration order.</param>
    /// <param name="directory">The party directory, which names a party no source named.</param>
    public PartyBillingProfileService(IEnumerable<IPartyBillingProfileSource> sources, IPartyDirectory directory)
    {
        _sources = sources ?? [];
        _directory = directory;
    }

    /// <inheritdoc/>
    public async Task<PartyBillingProfile> GetAsync(PartyRef party, CancellationToken cancellationToken = default)
    {
        if (party.IsEmpty)
        {
            return null;
        }

        var profile = new PartyBillingProfile
        {
            Party = party,
        };

        foreach (var source in _sources)
        {
            if (source is null || !string.Equals(source.Kind, party.Kind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await source.ContributeAsync(party, profile, cancellationToken);
        }

        profile.Contact ??= new();
        profile.CopyTo ??= [];

        if (string.IsNullOrWhiteSpace(profile.Contact.Name) &&
            _directory is not null &&
            await _directory.GetNameAsync(party, cancellationToken) is { Length: > 0 } name)
        {
            profile.Contact = profile.Contact with { Name = name };
        }

        return profile;
    }
}
