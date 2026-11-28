using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Controllers;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Argument reading, JSON, paging, times and visit lookups for the dispatch administration AI tools.
/// </summary>
internal static class DispatchAdminTools
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    public static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    public static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.TryGetValue(name, out var value) && value is not null && value is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>
    /// Reads a text argument, trimmed; empty text is <see langword="null"/>.
    /// </summary>
    public static string Read(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var value = arguments[name];
        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(Read(arguments, name), out var value) ? value : null;

    public static int? ReadInt(AIFunctionArguments arguments, string name)
    {
        var text = Read(arguments, name);

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue
            ? (int)real
            : null;
    }

    /// <summary>
    /// Reads a day (yyyy-MM-dd).
    /// </summary>
    public static DateOnly? ReadDate(AIFunctionArguments arguments, string name)
    {
        var text = Read(arguments, name);

        return DateOnly.TryParseExact(text?.Length > 10 ? text[..10] : text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    }

    /// <summary>
    /// Reads an enum value by name, ignoring case; numbers are refused, so a stray number never picks a value by position.
    /// </summary>
    public static bool TryReadEnum<TEnum>(AIFunctionArguments arguments, string name, out TEnum value)
        where TEnum : struct, Enum
    {
        var text = Read(arguments, name);

        return Enum.TryParse(text, ignoreCase: true, out value) && Enum.IsDefined(value) && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Gets the time zone dispatch shows times in, as the Visits page and the board do.
    /// </summary>
    public static Task<string> TimeZoneAsync(IServiceProvider services)
        => services.GetRequiredService<DispatchTimeZone>().GetAsync();

    /// <summary>
    /// Formats a stored UTC time in the dispatch time zone.
    /// </summary>
    public static string Local(DateTime? utc, string zone)
        => utc is { } value && value != default
            ? CalendarTime.ToLocal(DateTime.SpecifyKind(value, DateTimeKind.Utc), zone).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// Finds a visit by its ID, its number (V-000012) or its work order's number (the work order's open visit).
    /// </summary>
    public static async Task<ServiceVisit> FindVisitAsync(IServiceProvider services, string reference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        return await FieldServiceLookups.VisitAsync(services, reference, cancellationToken)
            ?? await services.GetRequiredService<IDispatchService>().FindAsync(reference.Trim(), cancellationToken);
    }

    /// <summary>
    /// Gives the technicians' names by their IDs.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, string>> WorkerNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var workers = await services.GetRequiredService<DispatchResources>().ListWorkersAsync(cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var worker in workers)
        {
            names[worker.ItemId] = worker.DisplayName;
        }

        return names;
    }

    /// <summary>
    /// The short form of a visit, as a list row shows it.
    /// </summary>
    public static object Row(ServiceVisit visit, IReadOnlyDictionary<string, string> workerNames, string zone)
        => new
        {
            id = visit.ItemId,
            number = visit.Number,
            workOrderNumber = visit.WorkOrderNumber,
            summary = visit.Summary,
            status = visit.Status.ToString(),
            priority = visit.Priority.ToString(),
            technicians = visit.WorkerIds.Select(id => workerNames.GetValueOrDefault(id, id)).ToArray(),
            start = Local(visit.StartUtc, zone),
            end = Local(visit.EndUtc, zone),
            due = Local(visit.DueUtc, zone),
            pinned = visit.Pinned,
            atRisk = visit.AtRiskReason,
        };

    /// <summary>
    /// What a write tool reports about the visit it changed.
    /// </summary>
    public static object Changed(ServiceVisit visit, IReadOnlyDictionary<string, string> workerNames, string zone)
        => new
        {
            id = visit.ItemId,
            number = visit.Number,
            status = visit.Status.ToString(),
            technicians = visit.WorkerIds.Select(id => workerNames.GetValueOrDefault(id, id)).ToArray(),
            start = Local(visit.StartUtc, zone),
            end = Local(visit.EndUtc, zone),
            pinned = visit.Pinned,
            returnVisitId = visit.ReturnVisitId,
            nextStatuses = VisitStatusRules.Next(visit.Status).Select(status => status.ToString()).ToArray(),
        };

    /// <summary>
    /// Saves the changes now, as the other dispatch tools do, so a later call in the same chat turn reads them.
    /// </summary>
    public static Task SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);
}
