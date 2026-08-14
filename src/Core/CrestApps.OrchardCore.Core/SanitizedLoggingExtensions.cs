namespace CrestApps.Core.Support;

/// <summary>
/// Provides helpers for rendering untrusted values safely in structured logs.
/// </summary>
public static class SanitizedLoggingExtensions
{
    /// <summary>
    /// Removes control characters so log lines cannot be broken or forged by user input.
    /// </summary>
    /// <param name="value">The value to sanitize for log output.</param>
    /// <returns>A log-safe string value.</returns>
    public static string SanitizeLogValue(this string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return string.Create(value.Length, value, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsControl(source[i]) ? ' ' : source[i];
            }
        }).Trim();
    }
}
