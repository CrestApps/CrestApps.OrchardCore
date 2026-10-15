namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The view model backing the agent's "my voicemail greeting" page.
/// </summary>
public sealed class MyVoicemailGreetingViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the agent currently has a recorded or uploaded audio greeting.
    /// </summary>
    public bool HasAudioGreeting { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the greeting exists only at the phone provider it was uploaded to,
    /// because it was recorded before the platform kept master copies. Recording or uploading it again protects it.
    /// </summary>
    public bool IsProviderCopyOnly { get; set; }
}
