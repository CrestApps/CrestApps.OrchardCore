namespace CrestApps.OrchardCore.Workforce.Core;

/// <summary>
/// Normalizes names and numbers that must be unique whatever their case and spacing.
/// </summary>
public static class WorkforceNames
{
    /// <summary>
    /// Gets the form of a value used to compare it with others.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The trimmed value in upper case, or <see langword="null"/>.</returns>
    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
    }
}
