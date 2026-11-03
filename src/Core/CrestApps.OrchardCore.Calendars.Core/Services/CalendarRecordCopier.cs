using System.Reflection;
using System.Text.Json;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Deep-copies calendar records, so a change can be compared with the record as it was and undone when it is refused.
/// </summary>
internal static class CalendarRecordCopier
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.General);

    /// <summary>
    /// Makes a deep copy of a record.
    /// </summary>
    /// <typeparam name="T">The record's type.</typeparam>
    /// <param name="record">The record.</param>
    /// <returns>The copy, or <see langword="null"/> when the record is <see langword="null"/>.</returns>
    public static T Clone<T>(T record)
        where T : class
    {
        if (record is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(record, _options), _options);
    }

    /// <summary>
    /// Copies every public settable property of a record onto another instance, so a tracked instance can be put back as it was.
    /// </summary>
    /// <typeparam name="T">The record's type.</typeparam>
    /// <param name="source">The values to copy.</param>
    /// <param name="target">The instance to overwrite.</param>
    public static void CopyTo<T>(T source, T target)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var copy = Clone(source);

        foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(target, property.GetValue(copy));
            }
        }
    }
}
