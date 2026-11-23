using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.Core.Services;

/// <summary>
/// Which members of a list a page shows.
/// </summary>
public sealed class MailingListMemberQuery
{
    /// <summary>
    /// Gets the id of the list.
    /// </summary>
    public string ListId { get; init; }

    /// <summary>
    /// Gets the state the members are in, or <see langword="null"/> for every state.
    /// </summary>
    public MailingListMemberStatus? Status { get; init; }

    /// <summary>
    /// Gets the source the members joined through, or <see langword="null"/> for every source.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets a part of the address to look for, or <see langword="null"/>.
    /// </summary>
    public string Search { get; init; }

    /// <summary>
    /// Gets whether only members whose last change did not reach the provider are shown.
    /// </summary>
    public bool NotSyncedOnly { get; init; }
}
