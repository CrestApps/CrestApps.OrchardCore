namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

/// <summary>
/// Represents the view model for edit AI profile session settings.
/// </summary>
public class EditAIProfileSessionSettingsViewModel
{
    /// <summary>
    /// Gets or sets the session inactivity timeout in minutes.
    /// </summary>
    public int SessionInactivityTimeoutInMinutes { get; set; } = 30;
}
