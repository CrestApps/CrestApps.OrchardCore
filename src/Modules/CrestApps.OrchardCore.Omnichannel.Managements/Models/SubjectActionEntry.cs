using CrestApps.OrchardCore.Omnichannel.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Models;

/// <summary>
/// A subject action with the names it is shown by, as listed on the page that manages the flow of a subject.
/// </summary>
public sealed class SubjectActionEntry
{
    /// <summary>
    /// Gets or sets the subject action.
    /// </summary>
    public SubjectAction Action { get; set; }

    /// <summary>
    /// Gets or sets the disposition display text.
    /// </summary>
    public string DispositionDisplayText { get; set; }

    /// <summary>
    /// Gets or sets the action type display name.
    /// </summary>
    public string ActionTypeDisplayName { get; set; }
}
