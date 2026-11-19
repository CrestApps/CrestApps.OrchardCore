using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Accounts.Services;

/// <summary>
/// Bills an account (party kind <see cref="PartyKinds.Account"/>) under its name, at its billing location: the location
/// marked as the billing one, else its first location (see <see cref="AccountLocationsPart.GetBillingLocation"/>). The
/// published version of the account is read, or its latest version while it has never been published. Modules that keep
/// more about an account, such as its tax number or terms, add their own source after this one.
/// </summary>
public sealed class AccountBillingProfileSource : IPartyBillingProfileSource
{
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountBillingProfileSource"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    public AccountBillingProfileSource(IContentManager contentManager)
    {
        _contentManager = contentManager;
    }

    /// <inheritdoc/>
    public string Kind => PartyKinds.Account;

    /// <inheritdoc/>
    public async Task ContributeAsync(PartyRef party, PartyBillingProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (party.IsEmpty)
        {
            return;
        }

        var account = await _contentManager.GetAsync(party.Id, VersionOptions.Published)
            ?? await _contentManager.GetAsync(party.Id, VersionOptions.Latest);

        if (account is null || !account.Has<AccountPart>())
        {
            return;
        }

        var contact = profile.Contact ?? new ContactInfo();
        var location = account.TryGet<AccountLocationsPart>(out var locations) ? locations.GetBillingLocation() : null;

        profile.Contact = contact with
        {
            Name = string.IsNullOrWhiteSpace(account.DisplayText) ? contact.Name : account.DisplayText.Trim(),
            Address = location?.Address?.Clone() ?? contact.Address,
        };
    }
}
