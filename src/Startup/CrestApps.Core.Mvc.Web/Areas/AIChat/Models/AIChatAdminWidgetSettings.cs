namespace CrestApps.Core.Mvc.Web.Areas.AIChat.Models;

public sealed class AIChatAdminWidgetSettings
{
    public const string DefaultSecondaryColor = "#6c757d";

    public string ProfileId { get; set; }

    public string PrimaryColor { get; set; } = DefaultSecondaryColor;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ProfileId);
}
