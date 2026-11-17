using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// The kinds of pay code modules registered beyond the built-in ones.
/// </summary>
public sealed class PayCodeKindOptions
{
    /// <summary>
    /// Gets the kinds, by stored name.
    /// </summary>
    public Dictionary<string, LocalizationSource> Kinds { get; } = new(StringComparer.Ordinal);
}
