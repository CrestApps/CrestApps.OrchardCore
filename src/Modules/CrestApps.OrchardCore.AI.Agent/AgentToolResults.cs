using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.AI.Extensions;
using Microsoft.Extensions.AI;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// The result conventions of the agent tools: JSON with camel-case names, lists as
/// <c>{ items, total, page, pageSize }</c> with a capped page size, and dates in the user's or the site's time zone.
/// </summary>
internal static class AgentToolResults
{
    /// <summary>
    /// The page size a list tool uses when the caller gives none.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The largest page size a list tool returns.
    /// </summary>
    public const int MaxPageSize = 50;

    // The result is read by the model, not put in a page, so titles in any language stay as written instead of
    // becoming \uXXXX escapes.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Writes a tool result as JSON.
    /// </summary>
    /// <param name="value">The result, built from an explicit list of fields.</param>
    /// <returns>The JSON text.</returns>
    public static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments of a list tool.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <returns>The page, starting at 1, and the page size, between 1 and <see cref="MaxPageSize"/>.</returns>
    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var page = Math.Max(1, arguments.GetFirstValueOrDefault("page", 1));
        var pageSize = Math.Clamp(arguments.GetFirstValueOrDefault("pageSize", DefaultPageSize), 1, MaxPageSize);

        return (page, pageSize);
    }

    /// <summary>
    /// Converts a stored UTC time to the user's or the site's time zone.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="utc">The stored UTC time.</param>
    /// <returns>The local time with its offset, or <see langword="null"/> when there is no time.</returns>
    public static async Task<string> ToLocalAsync(ILocalClock localClock, DateTime? utc)
    {
        ArgumentNullException.ThrowIfNull(localClock);

        if (utc is null)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the name of the time zone the local times of a result are in.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <returns>The time zone ID.</returns>
    public static async Task<string> GetTimeZoneIdAsync(ILocalClock localClock)
    {
        ArgumentNullException.ThrowIfNull(localClock);

        return (await localClock.GetLocalTimeZoneAsync())?.TimeZoneId;
    }
}
