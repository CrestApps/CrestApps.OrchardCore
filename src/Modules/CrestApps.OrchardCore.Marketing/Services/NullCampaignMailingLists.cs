using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.Marketing.Services;

/// <summary>
/// The mailing lists of a site without the Mailing Lists feature: none, so campaigns go to segments only.
/// </summary>
internal sealed class NullCampaignMailingLists : ICampaignMailingLists
{
    /// <summary>
    /// The shared instance.
    /// </summary>
    public static readonly NullCampaignMailingLists Instance = new();

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    public Task<IReadOnlyList<MailingList>> ListEnabledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MailingList>>([]);

    /// <inheritdoc/>
    public Task<MailingList> FindAsync(string listId, CancellationToken cancellationToken = default)
        => Task.FromResult<MailingList>(null);
}
