using OrchardCore.Admin;
using OrchardCore.ContentManagement;

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
    /// Gets or sets the scheduled activities and their pager, rendered by an admin list.
    /// </summary>
    public AdminListContext ScheduledList { get; set; }

    /// <summary>
    /// Gets or sets the completed activities and their pager, rendered by an admin list.
    /// </summary>
    public AdminListContext CompletedList { get; set; }
}
