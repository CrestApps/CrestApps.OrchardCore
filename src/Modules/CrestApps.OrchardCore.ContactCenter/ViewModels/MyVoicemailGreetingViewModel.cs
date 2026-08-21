namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The view model backing the agent's "my voicemail greeting" page.
/// </summary>
public sealed class MyVoicemailGreetingViewModel
{
    /// <summary>
    /// Gets or sets the agent's spoken (text-to-speech) greeting.
    /// </summary>
    public string GreetingText { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent currently has a recorded or uploaded audio greeting.
    /// </summary>
    public bool HasAudioGreeting { get; set; }
}
