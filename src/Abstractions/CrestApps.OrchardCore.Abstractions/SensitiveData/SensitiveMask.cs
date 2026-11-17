namespace CrestApps.OrchardCore.SensitiveData;

/// <summary>
/// Writes sensitive values for display with all but their last characters hidden.
/// </summary>
public static class SensitiveMask
{
    private const string Hidden = "••••";

    /// <summary>
    /// Returns a stored value for display: a fixed mask followed by its hint, such as <c>••••6789</c>. The mask has the
    /// same length whatever the value's length, so it does not tell how long the value is.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The masked value, or an empty string when none is stored.</returns>
    public static string Mask(ProtectedValue value)
    {
        if (value is null || !value.HasValue)
        {
            return string.Empty;
        }

        return Hidden + value.Hint;
    }

    /// <summary>
    /// Returns a clear-text value for display with all but its last characters hidden.
    /// </summary>
    /// <param name="value">The value in clear text.</param>
    /// <param name="visibleCharacters">How many of the last characters to show. Never more than half of the value is shown.</param>
    /// <returns>The masked value, or an empty string when <paramref name="value"/> is empty.</returns>
    public static string MaskValue(string value, int visibleCharacters = 4)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return Hidden + LastCharacters(value, visibleCharacters);
    }

    /// <summary>
    /// Returns the last characters of a value that may be shown in clear text: as many as asked for, and never more
    /// than half of the value, so a short value is not given away by its hint.
    /// </summary>
    /// <param name="value">The value in clear text.</param>
    /// <param name="visibleCharacters">How many of the last characters to show.</param>
    /// <returns>The characters, or an empty string.</returns>
    public static string LastCharacters(string value, int visibleCharacters = 4)
    {
        if (string.IsNullOrEmpty(value) || visibleCharacters <= 0)
        {
            return string.Empty;
        }

        var count = Math.Min(visibleCharacters, value.Length / 2);

        return value.Substring(value.Length - count, count);
    }
}
