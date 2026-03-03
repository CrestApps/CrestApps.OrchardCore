namespace CrestApps.OrchardCore.AI.Chat.Settings;

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
    /// Gets or sets the maximum number of previous sessions to display in the widget history.
    /// </summary>
    public int MaxSessions { get; set; } = DefaultMaxSessions;

    /// <summary>
    /// Gets or sets the primary color used by the admin chat widget.
    /// </summary>
    public string PrimaryColor { get; set; } = DefaultPrimaryColor;
}
