namespace CrestApps.OrchardCore;

/// <summary>
/// Cuts text to the length of the column it is stored in, such as an index column. Import it with
/// <c>using static CrestApps.OrchardCore.StringTruncation;</c>.
/// </summary>
/// <remarks>
/// CrestApps.Core's <c>Str.Truncate</c> turns <see langword="null"/> into an empty string, which changes what an index
/// stores and what a <c>== null</c> query finds, so index providers use these instead.
/// </remarks>
public static class StringTruncation
{
    /// <summary>
    /// Cuts a value to at most <paramref name="maxLength"/> characters, keeping <see langword="null"/> and empty values
    /// as they are.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="maxLength">The longest value kept.</param>
    /// <returns>The value, cut to the length.</returns>
    public static string Truncate(string value, int maxLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);

        return value is null || value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Cuts a value to at most <paramref name="maxLength"/> characters, storing an empty value as
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="maxLength">The longest value kept.</param>
    /// <returns>The value, cut to the length, or <see langword="null"/> when it is empty.</returns>
    public static string TruncateOrNull(string value, int maxLength)
        => string.IsNullOrEmpty(value) ? null : Truncate(value, maxLength);
}
