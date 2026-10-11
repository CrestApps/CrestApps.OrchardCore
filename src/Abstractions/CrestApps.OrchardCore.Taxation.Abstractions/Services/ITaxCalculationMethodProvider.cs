namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Resolves a registered <see cref="ITaxCalculationMethod"/> by name.
/// </summary>
public interface ITaxCalculationMethodProvider
{
    /// <summary>
    /// Gets the calculation method registered under the supplied name.
    /// </summary>
    /// <param name="name">The name of the calculation method.</param>
    /// <returns>The matching calculation method, or <see langword="null"/> when none is registered.</returns>
    ITaxCalculationMethod GetMethod(string name);
}
