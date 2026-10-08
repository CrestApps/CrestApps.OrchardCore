using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Context passed to <see cref="Services.ISubjectActionExecutor"/> when processing subject actions.
/// </summary>
public sealed class SubjectActionExecutionContext
{
    /// <summary>
    /// Gets or sets the activity being completed.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

    /// <summary>
    /// Gets or sets the contact content item.
    /// </summary>
    public ContentItem Contact { get; set; }

    /// <summary>
    /// Gets or sets the subject content item.
    /// </summary>
    public ContentItem Subject { get; set; }

    /// <summary>
    /// Gets or sets the selected disposition.
    /// </summary>
    public OmnichannelDisposition Disposition { get; set; }

    /// <summary>
    /// Gets or sets the schedule dates provided by the user during completion.
    /// Key is the subject action ItemId, value is the schedule date.
    /// </summary>
    public IDictionary<string, DateTime?> ActionScheduleDates { get; set; }

    /// <summary>
    /// Gets or sets the optional preparation notes provided by the user during completion. Each note becomes the
    /// <see cref="OmnichannelActivity.Instructions"/> of the follow-up activity created by the matching subject action.
    /// Key is the subject action ItemId, value is the preparation note.
    /// </summary>
    public IDictionary<string, string> ActionPreparationNotes { get; set; }

    /// <summary>
    /// Gets or sets what found the number out of service when the platform chose a not-in-service disposition on its
    /// own, one of <see cref="OmnichannelConstants.NotInServiceSources"/>. It has already marked the number it dialed,
    /// so the disposition's outcome does not mark it again. Not set when a person chose the disposition.
    /// </summary>
    public string NotInServiceSource { get; set; }
}
