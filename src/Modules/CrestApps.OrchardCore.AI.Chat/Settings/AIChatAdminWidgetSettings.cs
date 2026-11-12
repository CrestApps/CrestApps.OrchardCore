namespace CrestApps.OrchardCore.AI.Chat.Settings;

/// <summary>
/// Represents the AI chat admin widget settings.
/// </summary>
public sealed class AIChatAdminWidgetSettings
{
    public const string DefaultPrimaryColor = "#41b670";
    public const int DefaultMaxSessions = 10;
    public const int MinMaxSessions = 1;
    public const int MaxMaxSessions = 50;

    /// <summary>
    /// Gets or sets the AI profile ID to use for the admin widget.
    /// </summary>
    public string ProfileId { get; set; }

    /// <summary>
    /// Gets a value indicating whether the admin widget is enabled.
    /// </summary>
    public bool IsEnabled => !string.IsNullOrWhiteSpace(ProfileId);

    /// <summary>
    /// Gets or sets the maximum number of previous sessions to display in the widget history.
    /// </summary>
    public int MaxSessions { get; set; } = DefaultMaxSessions;

    /// <summary>
    /// Gets or sets the primary color used by the admin chat widget.
    /// </summary>
    public string PrimaryColor { get; set; } = DefaultPrimaryColor;

    /// <summary>
    /// Gets or sets a value indicating whether the widget tells the AI which page the user is on with each message:
    /// the page title and address, the text the user selected, the content item being edited and the site.
    /// </summary>
    public bool SendPageContext { get; set; } = true;
}
