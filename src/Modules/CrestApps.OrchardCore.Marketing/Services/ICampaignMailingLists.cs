using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.Marketing.Services;

/// <summary>
/// The mailing lists a campaign can be sent to. Marketing registers a default that has none; while the Mailing Lists
/// feature is on, the bridge startup replaces it with one that reads the lists, so the campaign editor and the recipe
/// step work as before without the feature.
/// </summary>
public interface ICampaignMailingLists
{
    /// <summary>
    /// Gets whether campaigns can go to a mailing list: the Mailing Lists feature is on.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns the lists the campaign editor offers: the enabled ones, by name.
    /// </summary>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The lists, or none while the Mailing Lists feature is off.</returns>
    Task<IReadOnlyList<MailingList>> ListEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a list by its id, enabled or not.
    /// </summary>
    /// <param name="listId">The list's id.</param>
    /// <param name="cancellationToken">The token that cancels the operation.</param>
    /// <returns>The list, or <see langword="null"/> when there is none or the Mailing Lists feature is off.</returns>
    Task<MailingList> FindAsync(string listId, CancellationToken cancellationToken = default);
}
