namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// The settings of a local delivery method: the postal codes it delivers to and what it costs.
/// </summary>
public sealed class LocalDeliverySettings
{
    /// <summary>
    /// Gets or sets the postal codes delivered to, one per entry; one ending in <c>*</c> covers every code starting
    /// with what's before it, such as <c>981*</c>.
    /// </summary>
    public List<string> PostalCodes { get; set; } = [];

    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the order subtotal from which delivery is free, or <see langword="null"/>.
    /// </summary>
    public decimal? FreeFromSubtotal { get; set; }

    /// <summary>
    /// Gets or sets the smallest order subtotal delivered.
    /// </summary>
    public decimal MinimumSubtotal { get; set; }

    /// <summary>
    /// Returns whether the postal code is one the method delivers to, ignoring case and spaces.
    /// </summary>
    public bool Covers(string postalCode)
    {
        var code = Normalize(postalCode);

        if (code.Length == 0)
        {
            return false;
        }

        foreach (var entry in PostalCodes ?? [])
        {
            var pattern = Normalize(entry);

            if (pattern.EndsWith('*') ? code.StartsWith(pattern[..^1], StringComparison.Ordinal) && pattern.Length > 1 : code == pattern)
            {
                return true;
            }
        }

        return false;
    }

    private static string Normalize(string value)
        => string.Concat((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '-')).ToUpperInvariant();
}
