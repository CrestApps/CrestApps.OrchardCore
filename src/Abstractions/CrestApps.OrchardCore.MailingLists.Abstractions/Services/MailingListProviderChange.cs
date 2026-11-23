using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// A change a list's provider reported about one address, from a webhook or the reconciliation.
/// </summary>
public sealed class MailingListProviderChange
{
    /// <summary>
    /// Gets the id of the list on the site.
    /// </summary>
    public string ListId { get; init; }

    /// <summary>
    /// Gets the address the change is about.
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    /// Gets the new address, when the provider reports that the person changed their address.
    /// </summary>
    public string NewEmail { get; init; }

    /// <summary>
    /// Gets the member's new state, or <see langword="null"/> when only the profile changed.
    /// </summary>
    public MailingListMemberStatus? Status { get; init; }

    /// <summary>
    /// Gets whether the provider confirmed the address (its own double opt-in).
    /// </summary>
    public bool Verified { get; init; }

    /// <summary>
    /// Gets the person's name, when the provider reports it.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the member's id at the provider.
    /// </summary>
    public string ProviderMemberId { get; init; }

    /// <summary>
    /// Gets why the person left, when the provider reports it.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets when the change happened at the provider (UTC); older changes than the member's last status change are ignored.
    /// </summary>
    public DateTime OccurredUtc { get; init; }
}
