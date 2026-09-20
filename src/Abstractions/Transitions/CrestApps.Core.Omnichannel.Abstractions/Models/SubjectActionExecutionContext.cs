using CrestApps.Core.Omnichannel.Models;

namespace CrestApps.Core.Omnichannel.Models;

/// <summary>
/// Context passed to <see cref="Services.ISubjectActionExecutor"/> when processing subject actions.
/// </summary>
/// <remarks>
/// The contact and the subject are not carried here. The activity names the contact and carries the
/// subject, so an executor resolves both from it - which keeps a caller from handing over a stale copy
/// of either, and keeps this context free of whatever the host stores them as.
/// </remarks>
public sealed class SubjectActionExecutionContext
{
    /// <summary>
    /// Gets or sets the activity being completed.
    /// </summary>
    public OmnichannelActivity Activity { get; set; }

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
}
