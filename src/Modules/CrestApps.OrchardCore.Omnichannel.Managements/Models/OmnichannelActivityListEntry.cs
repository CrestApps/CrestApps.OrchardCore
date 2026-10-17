using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// A row of an admin list of activities: the activity container and how the list shows it. The row is built by
/// <see cref="Services.OmnichannelActivityListEntryDisplayManager"/>, as the <c>OmnichannelActivityContainer</c> shape.
/// </summary>
internal sealed class OmnichannelActivityListEntry
{
    /// <summary>
    /// Gets the activity, its contact, subject and user.
    /// </summary>
    public OmnichannelActivityContainer Container { get; init; }

    /// <summary>
    /// Gets the display group of the row, e.g. <c>ScheduledActivity</c> or <c>CompletedActivity</c>, or
    /// <see langword="null"/> for the row of the activity lists that are not about one contact.
    /// </summary>
    public string GroupId { get; init; }

    /// <summary>
    /// Gets whether the row carries the checkbox the bulk actions of its list select it with.
    /// </summary>
    public bool Selectable { get; init; }

    /// <summary>
    /// Gets whether the row names the contact of the activity, for a list that holds the activities of several
    /// contacts, e.g. those of an account.
    /// </summary>
    public bool ShowContact { get; init; }
}
