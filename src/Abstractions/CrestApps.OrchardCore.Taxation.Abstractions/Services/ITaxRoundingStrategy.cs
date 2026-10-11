namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Rounds tax amounts using an explicit policy so that results do not depend on incidental decimal behavior.
/// </summary>
public interface ITaxRoundingStrategy
{
    /// <summary>
    /// Rounds the supplied value for the supplied currency.
    /// </summary>
    /// <param name="value">The value to round.</param>
    /// <param name="currency">The currency the value is expressed in.</param>
    /// <returns>The rounded value.</returns>
    decimal Round(decimal value, string currency);
}
