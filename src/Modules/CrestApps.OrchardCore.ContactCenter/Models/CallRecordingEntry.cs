using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// A recorded call as the call recordings list shows it: the recording, with the names it is read by.
/// </summary>
public sealed class CallRecordingEntry
{
    /// <summary>
    /// Gets or sets the recording.
    /// </summary>
    public CallRecording Recording { get; set; }

    /// <summary>
    /// Gets or sets the name of the agent on the call, or <see langword="null"/> when no person took part.
    /// </summary>
    public string AgentName { get; set; }

    /// <summary>
    /// Gets or sets the customer's number formatted for reading, or <see langword="null"/> when it is not known.
    /// </summary>
    public string CustomerNumber { get; set; }
}
