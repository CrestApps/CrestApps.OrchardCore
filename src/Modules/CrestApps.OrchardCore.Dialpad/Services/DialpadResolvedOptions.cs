namespace CrestApps.OrchardCore.Dialpad.Services;

/// <summary>
/// Provides the active Dialpad settings resolved once for the current tenant shell.
/// </summary>
public sealed class DialpadResolvedOptions
{
    /// <summary>
    /// Gets or sets the active Dialpad settings with protected values resolved.
    /// </summary>
    internal DialpadResolvedSettings Settings { get; set; }
}
