using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Calendars;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// What the omnichannel tools share on top of <see cref="AIModuleTools"/>: the admin links of this module, reading
/// enums, lists and local times from the arguments, and turning a failed validation into a sentence.
/// </summary>
internal static class OmnichannelToolSupport
{
    /// <summary>
    /// The area of the omnichannel admin pages: this module.
    /// </summary>
    public static readonly string Area = typeof(OmnichannelToolSupport).Assembly.GetName().Name;

    /// <summary>
    /// Gets the path of an admin page of this module.
    /// </summary>
    public static string GetPath(IServiceProvider services, string controller, string action, string id = null)
        => AIModuleTools.GetAdminPath(
            services,
            Area,
            controller,
            action,
            id is null ? null : new RouteValueDictionary { ["id"] = id });

    /// <summary>
    /// Writes the reasons a validation failed as one sentence.
    /// </summary>
    public static string Reasons(ValidationResultDetails validation)
        => string.Join(" ", validation.Errors
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal));

    /// <summary>
    /// Reads an optional enum argument by name, in any letter case. Numbers are refused so a model cannot pass a value
    /// the enum does not name.
    /// </summary>
    /// <returns><see langword="false"/> when the argument is given but names no value.</returns>
    public static bool TryReadEnum<TEnum>(AIFunctionArguments arguments, string key, out TEnum? value)
        where TEnum : struct, Enum
    {
        value = null;

        var text = AIModuleTools.ReadString(arguments, key);

        if (text is null)
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
            Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed))
        {
            value = parsed;

            return true;
        }

        return false;
    }

    /// <summary>
    /// The names of an enum, for an error sentence.
    /// </summary>
    public static string Names<TEnum>()
        where TEnum : struct, Enum
        => string.Join(", ", Enum.GetNames<TEnum>());

    /// <summary>
    /// Reads an optional list of texts. A model sends it as an array or as a single text.
    /// </summary>
    /// <returns>The trimmed, distinct, non-blank values, or <see langword="null"/> when the argument is not given.</returns>
    public static string[] ReadStrings(AIFunctionArguments arguments, string key)
    {
        if (!arguments.TryGetFirst(key, out var value) || value is null)
        {
            return null;
        }

        IEnumerable<string> values = value switch
        {
            string text => [text],
            JsonElement { ValueKind: JsonValueKind.String } element => [element.GetString()],
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString()),
            JsonArray array => array.Select(item => item?.ToString()),
            IEnumerable<string> texts => texts,
            IEnumerable<object> items => items.Select(item => item?.ToString()),
            _ => [value.ToString()],
        };

        return values
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Reads an optional date and time in the user's or the site's time zone and converts it to UTC, as the admin
    /// editors do.
    /// </summary>
    /// <returns><see langword="false"/> when the argument is given but is not a date and time.</returns>
    public static async Task<(bool Valid, DateTime? Utc)> TryReadLocalDateTimeAsync(IServiceProvider services, AIFunctionArguments arguments, string key)
    {
        var text = AIModuleTools.ReadString(arguments, key);

        if (text is null)
        {
            return (true, null);
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
        {
            return (false, null);
        }

        // An offset in the text is the model's statement of the time zone; without one the time is the user's local time.
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var withOffset) &&
            HasOffset(text))
        {
            return (true, withOffset.UtcDateTime);
        }

        var localClock = services.GetRequiredService<ILocalClock>();

        return (true, await localClock.ToUtcLenientAsync(DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified)));
    }

    private static bool HasOffset(string text)
    {
        var timeStart = text.IndexOf('T', StringComparison.Ordinal);

        if (timeStart < 0)
        {
            timeStart = text.IndexOf(' ', StringComparison.Ordinal);
        }

        if (timeStart < 0)
        {
            return false;
        }

        var time = text[timeStart..];

        return time.EndsWith('Z') || time.Contains('+', StringComparison.Ordinal) || time.LastIndexOf('-') > 0;
    }
}
