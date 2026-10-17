namespace CrestApps.OrchardCore.Stripe.Models;

/// <summary>
/// Holds the Stripe values a tenant reads from its application configuration (the <c>CrestApps_Stripe</c> section of
/// <c>IShellConfiguration</c>), such as <c>appsettings.json</c>, environment variables, user secrets or a secret store.
/// </summary>
/// <remarks>
/// Every value is optional. A value that is present wins over the value stored on the <b>Settings &gt; Payments &gt;
/// Stripe</b> page, one value at a time, and the page shows it as configured from the application settings and does not
/// overwrite it. A value that is missing falls back to the stored setting.
/// </remarks>
public sealed class StripeConfigurationOptions
{
    /// <summary>
    /// The name of the tenant configuration section the options are bound from.
    /// </summary>
    public const string SectionName = "CrestApps_Stripe";

    /// <summary>
    /// Gets or sets a value indicating whether the live keys are used. When <see langword="null"/>, the value stored on
    /// the settings page decides.
    /// </summary>
    public bool? IsLive { get; set; }

    /// <summary>
    /// Gets or sets the keys of the Stripe test mode.
    /// </summary>
    public StripeConfigurationKeys Test { get; set; } = new();

    /// <summary>
    /// Gets or sets the keys of the Stripe live mode.
    /// </summary>
    public StripeConfigurationKeys Live { get; set; } = new();

    /// <summary>
    /// Gets the keys configured for the given mode.
    /// </summary>
    /// <param name="isLive"><see langword="true"/> for the live mode; otherwise the test mode.</param>
    /// <returns>The configured keys of the mode, never <see langword="null"/>.</returns>
    public StripeConfigurationKeys GetKeys(bool isLive)
        => (isLive ? Live : Test) ?? new StripeConfigurationKeys();
}
