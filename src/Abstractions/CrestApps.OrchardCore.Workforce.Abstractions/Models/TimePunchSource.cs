namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a punch came from.
/// </summary>
public enum TimePunchSource
{
    /// <summary>
    /// The worker's own browser.
    /// </summary>
    Web,

    /// <summary>
    /// The installed My Work app.
    /// </summary>
    Mobile,

    /// <summary>
    /// A shared time clock device.
    /// </summary>
    Kiosk,

    /// <summary>
    /// The worker's contact center sign-in and sign-out.
    /// </summary>
    ContactCenter,

    /// <summary>
    /// Another system, through the API.
    /// </summary>
    Api,

    /// <summary>
    /// A manager, adding or correcting punches.
    /// </summary>
    Manager,
}
