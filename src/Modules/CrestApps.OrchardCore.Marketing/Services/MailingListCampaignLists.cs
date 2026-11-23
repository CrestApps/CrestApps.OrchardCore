using CrestApps.OrchardCore.MailingLists.Models;
using CrestApps.OrchardCore.MailingLists.Services;

namespace CrestApps.OrchardCore.Marketing.Services;

/// <summary>
/// The mailing lists a campaign can be sent to, read through the mailing lists building block.
/// </summary>
internal sealed class MailingListCampaignLists : ICampaignMailingLists
{
    private readonly IMailingListManager _lists;

    /// <summary>
    /// Initializes a new instance of the <see cref="MailingListCampaignLists"/> class.
    /// </summary>
    /// <param name="lists">The mailing list manager.</param>
    public MailingListCampaignLists(IMailingListManager lists)
    {
        _lists = lists;
    }

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MailingList>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var lists = await _lists.GetAllAsync(cancellationToken);

        return [.. lists
            .Where(list => list.IsEnabled)
            .OrderBy(list => list.DisplayText ?? list.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    /// <inheritdoc/>
    public async Task<MailingList> FindAsync(string listId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(listId))
        {
            return null;
        }

        return await _lists.FindByIdAsync(listId, cancellationToken);
    }
}
