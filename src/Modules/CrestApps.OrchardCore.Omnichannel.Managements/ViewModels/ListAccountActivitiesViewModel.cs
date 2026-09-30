using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the activities of every contact in an account.
/// </summary>
public sealed class ListAccountActivitiesViewModel
{
    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public ContentItem Account { get; set; }

    /// <summary>
    /// Gets the scheduled activities.
    /// </summary>
    public List<AccountActivityEntry> Scheduled { get; } = [];

    /// <summary>
    /// Gets or sets the pager of the scheduled activities.
    /// </summary>
    public IShape ScheduledPager { get; set; }

    /// <summary>
    /// Gets the completed activities.
    /// </summary>
    public List<AccountActivityEntry> Completed { get; } = [];

    /// <summary>
    /// Gets or sets the pager of the completed activities.
    /// </summary>
    public IShape CompletedPager { get; set; }
}

/// <summary>
/// Represents one activity of an account and the contact it belongs to.
/// </summary>
public sealed class AccountActivityEntry
{
    /// <summary>
    /// Gets or sets the contact the activity is for.
    /// </summary>
    public ContentItem Contact { get; set; }

    /// <summary>
    /// Gets or sets the activity summary shape.
    /// </summary>
    public IShape Shape { get; set; }
}
