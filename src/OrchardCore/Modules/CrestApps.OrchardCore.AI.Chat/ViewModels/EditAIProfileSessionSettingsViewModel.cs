namespace CrestApps.OrchardCore.AI.Chat.ViewModels;

public class EditAIProfileSessionSettingsViewModel
{
    public int SessionInactivityTimeoutInMinutes { get; set; } = 30;

    public bool EnableAIResolutionDetection { get; set; }
}
