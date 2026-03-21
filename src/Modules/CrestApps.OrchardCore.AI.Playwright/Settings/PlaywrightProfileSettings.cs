namespace CrestApps.OrchardCore.AI.Playwright.Settings;

/// <summary>
/// Legacy per-profile Playwright settings stored in
/// <see cref="CrestApps.OrchardCore.AI.Models.AIProfile.Settings"/>.
/// Only the enabled flag is still used.
/// </summary>
public sealed class PlaywrightProfileSettings
{
    public bool Enabled { get; set; }
}
