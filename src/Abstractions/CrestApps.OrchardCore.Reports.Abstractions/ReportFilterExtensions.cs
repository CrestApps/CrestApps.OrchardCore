using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Reports.Models;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Provides typed read and write helpers over the extensible <see cref="ReportFilter"/> property bag,
/// including convenience accessors for the built-in date-range filter.
/// </summary>
public static class ReportFilterExtensions
{
    /// <summary>
    /// The property key that stores the inclusive lower UTC bound of the reporting period.
    /// </summary>
    public const string FromUtcKey = "FromUtc";

    /// <summary>
    /// The property key that stores the inclusive upper UTC bound of the reporting period.
    /// </summary>
    public const string ToUtcKey = "ToUtc";

    /// <summary>
    /// The property key that stores the selected date-range preset key.
    /// </summary>
    public const string DateRangeKey = "DateRangeKey";

    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Attempts to read a typed value from the filter property bag.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the stored value into.</typeparam>
    /// <param name="filter">The report filter.</param>
    /// <param name="key">The property key.</param>
    /// <param name="value">The deserialized value when the key is present and readable; otherwise the default of <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> when a value for <paramref name="key"/> was found and deserialized; otherwise <see langword="false"/>.</returns>
    public static bool TryGet<T>(this ReportFilter filter, string key, out T value)
    {
        ArgumentNullException.ThrowIfNull(filter);

        value = default;

        if (string.IsNullOrEmpty(key) ||
            filter.Properties is null ||
            !filter.Properties.TryGetPropertyValue(key, out var node) ||
            node is null)
        {
            return false;
        }

        try
        {
            value = node.Deserialize<T>(_serializerOptions);

            return true;
        }
        catch (JsonException)
        {
            value = default;

            return false;
        }
    }

    /// <summary>
    /// Reads a typed value from the filter property bag, returning the default when it is absent.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the stored value into.</typeparam>
    /// <param name="filter">The report filter.</param>
    /// <param name="key">The property key.</param>
    /// <returns>The deserialized value, or the default of <typeparamref name="T"/> when absent.</returns>
    public static T GetOrDefault<T>(this ReportFilter filter, string key)
    {
        return filter.TryGet<T>(key, out var value) ? value : default;
    }

    /// <summary>
    /// Writes a typed value to the filter property bag, or removes the key when the value is <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of value to serialize.</typeparam>
    /// <param name="filter">The report filter.</param>
    /// <param name="key">The property key.</param>
    /// <param name="value">The value to store, or <see langword="null"/> to remove the key.</param>
    public static void Set<T>(this ReportFilter filter, string key, T value)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (value is null || (value is string text && string.IsNullOrEmpty(text)))
        {
            filter.Properties.Remove(key);

            return;
        }

        filter.Properties[key] = JsonSerializer.SerializeToNode(value, _serializerOptions);
    }

    /// <summary>
    /// Removes a value from the filter property bag.
    /// </summary>
    /// <param name="filter">The report filter.</param>
    /// <param name="key">The property key to remove.</param>
    public static void Remove(this ReportFilter filter, string key)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentException.ThrowIfNullOrEmpty(key);

        filter.Properties?.Remove(key);
    }

    /// <summary>
    /// Reads the resolved date range stored by the built-in date-range filter.
    /// </summary>
    /// <param name="filter">The report filter.</param>
    /// <returns>The resolved date range. Its bounds are unset when the date-range filter was not contributed.</returns>
    public static ReportDateRange GetDateRange(this ReportFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return new ReportDateRange
        {
            FromUtc = filter.TryGet<DateTime>(FromUtcKey, out var from)
                ? DateTime.SpecifyKind(from, DateTimeKind.Utc)
                : null,
            ToUtc = filter.TryGet<DateTime>(ToUtcKey, out var to)
                ? DateTime.SpecifyKind(to, DateTimeKind.Utc)
                : null,
            Key = filter.GetOrDefault<string>(DateRangeKey),
        };
    }

    /// <summary>
    /// Writes the resolved date range to the filter property bag.
    /// </summary>
    /// <param name="filter">The report filter.</param>
    /// <param name="range">The date range to store.</param>
    public static void SetDateRange(this ReportFilter filter, ReportDateRange range)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(range);

        filter.Set(FromUtcKey, range.FromUtc);
        filter.Set(ToUtcKey, range.ToUtc);
        filter.Set(DateRangeKey, range.Key);
    }
}
