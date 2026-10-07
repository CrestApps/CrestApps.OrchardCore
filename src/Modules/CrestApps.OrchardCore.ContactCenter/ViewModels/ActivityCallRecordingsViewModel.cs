using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The recorded calls of a CRM activity that the viewer may hear, shown on the activity's page.
/// </summary>
public class ActivityCallRecordingsViewModel
{
    /// <summary>
    /// Gets or sets the recordings, oldest first.
    /// </summary>
    public IList<ActivityCallRecordingItemViewModel> Items { get; set; } = [];
}

/// <summary>
/// One recorded call on an activity's page.
/// </summary>
public class ActivityCallRecordingItemViewModel
{
    /// <summary>
    /// Gets or sets the recording.
    /// </summary>
    public CallRecording Recording { get; set; }

    /// <summary>
    /// Gets or sets when the recording started, in the viewer's time zone.
    /// </summary>
    public DateTime StartedLocal { get; set; }

    /// <summary>
    /// Gets or sets the name of the agent on the call, when a person took it.
    /// </summary>
    public string AgentName { get; set; }
}
