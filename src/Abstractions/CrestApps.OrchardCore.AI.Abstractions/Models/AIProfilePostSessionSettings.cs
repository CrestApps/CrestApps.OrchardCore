namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Settings for post-session close processing, stored on <see cref="AIProfile.Settings"/>.
/// When enabled, the system runs AI-powered analysis on the complete conversation
/// transcript after a session is closed.
/// </summary>
public class AIProfilePostSessionSettings
{
    /// <summary>
    /// Gets or sets whether post-session processing is enabled for this profile.
    /// </summary>
    public bool EnablePostSessionProcessing { get; set; }

    /// <summary>
    /// Gets or sets the list of post-session processing tasks to execute when a session closes.
    /// </summary>
    public List<PostSessionTask> PostSessionTasks { get; set; } = [];
}
