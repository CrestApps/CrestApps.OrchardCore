using System.Globalization;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The well-known places a punch comes from. A source is an open string: a module that punches workers in and out (such
/// as the contact center bridge) uses its own source name and describes it through an
/// <see cref="Services.ITimePunchSourceProvider"/>, without changing Workforce.
/// </summary>
public static class TimePunchSource
{
    /// <summary>
    /// The worker's own browser.
    /// </summary>
    public const string Web = "Web";

    /// <summary>
    /// The installed My Work app.
    /// </summary>
    public const string Mobile = "Mobile";

    /// <summary>
    /// A shared time clock device.
    /// </summary>
    public const string Kiosk = "Kiosk";

    /// <summary>
    /// Another system, through the API.
    /// </summary>
    public const string Api = "Api";

    /// <summary>
    /// A manager, adding or correcting punches.
    /// </summary>
    public const string Manager = "Manager";

    /// <summary>
    /// Returns the source a number stood for when sources were an enum, so punches and policies saved then are still
    /// read.
    /// </summary>
    /// <param name="value">The number the source was stored as.</param>
    /// <returns>The source name.</returns>
    public static string FromLegacyValue(long value)
        => value switch
        {
            0 => Web,
            1 => Mobile,
            2 => Kiosk,

            // The contact center's sign-in and sign-out, which the Workforce Contact Center bridge now names itself.
            3 => "ContactCenter",
            4 => Api,
            5 => Manager,
            _ => value.ToString(CultureInfo.InvariantCulture),
        };
}
