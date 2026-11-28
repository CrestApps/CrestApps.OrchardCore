using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Argument reading, paging, JSON, times, scope and admin links for the workforce administration AI tools.
/// </summary>
internal static class WorkforceAdminTools
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    /// <summary>
    /// The most records one bulk decision acts on.
    /// </summary>
    public const int MaxBulk = 20;

    public const string ModuleArea = "CrestApps.OrchardCore.Workforce";

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

    public static decimal? ReadDecimal(AIFunctionArguments arguments, string name)
        => decimal.TryParse(Read(arguments, name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Reads a day (yyyy-MM-dd).
    /// </summary>
    public static DateOnly? ReadDate(AIFunctionArguments arguments, string name)
    {
        var text = Read(arguments, name);

        return DateOnly.TryParseExact(text?.Length > 10 ? text[..10] : text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    }

    /// <summary>
    /// Reads a time of day (HH:mm).
    /// </summary>
    public static TimeOnly? ReadTime(AIFunctionArguments arguments, string name)
        => TimeOnly.TryParseExact(Read(arguments, name), ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    /// <summary>
    /// Reads a list of texts, given as an array or a comma separated text; empty when it is missing.
    /// </summary>
    public static IList<string> ReadList(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return [];
        }

        var value = arguments[name];
        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value, _jsonOptions);

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        return (Read(arguments, name) ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Reads an enum value by name, ignoring case.
    /// </summary>
    public static bool TryReadEnum<TEnum>(AIFunctionArguments arguments, string name, out TEnum value)
        where TEnum : struct, Enum
    {
        var text = Read(arguments, name);

        // Numbers are refused, so a stray number never picks a value by position.
        return Enum.TryParse(text, ignoreCase: true, out value) && Enum.IsDefined(value) && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Builds a page of a list result.
    /// </summary>
    public static object Paged<T>(IReadOnlyList<T> items, int total, int page, int pageSize)
        => new
        {
            items,
            total,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < total,
        };

    public static string Day(DateOnly? date)
        => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly? time)
        => time?.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a stored UTC time in the user's or the site's time zone.
    /// </summary>
    public static async Task<string> LocalAsync(ILocalClock localClock, DateTime? utc)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc)));

        return local.ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Formats a stored UTC time in a worker's own time zone, as the timesheet page shows punches.
    /// </summary>
    public static string InZone(DateTime? utc, string timeZoneId)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        return CalendarTime.ToLocal(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc), timeZoneId).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Returns the site's local date.
    /// </summary>
    public static async Task<DateOnly> TodayAsync(IServiceProvider services)
        => DateOnly.FromDateTime((await services.GetRequiredService<ILocalClock>().GetLocalNowAsync()).DateTime);

    /// <summary>
    /// Reads the team filter (<c>teamName</c>, a team's name or ID) and returns the teams to look in: the named team and
    /// its sub-teams the user manages, or every team the user manages (<see langword="null"/> when they manage all).
    /// </summary>
    public static async Task<(IReadOnlyCollection<string> TeamIds, string Error)> TeamFilterAsync(IServiceProvider services, ClaimsPrincipal user, AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        if (!scope.All && scope.TeamIds.Count == 0)
        {
            return (null, "The user doesn't manage any team, so there is nothing to show or change.");
        }

        var teamName = Read(arguments, "teamName");

        if (teamName is null)
        {
            return (scope.All ? null : scope.TeamIds.ToArray(), null);
        }

        var teamManager = services.GetRequiredService<ITeamManager>();
        var team = (await teamManager.ListTreeAsync(cancellationToken))
            .Select(entry => entry.Team)
            .FirstOrDefault(item => item.ItemId == teamName || WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(teamName));

        if (team is null)
        {
            return (null, $"No team is called '{teamName}'.");
        }

        if (!scope.Covers(team.ItemId))
        {
            return (null, $"The user doesn't manage {team.Name}.");
        }

        var subtree = await teamManager.GetSubtreeIdsAsync(team.ItemId, cancellationToken);

        return (subtree.Where(scope.Covers).Append(team.ItemId).Distinct(StringComparer.Ordinal).ToArray(), null);
    }

    /// <summary>
    /// Finds the worker named by <c>workerId</c> or <c>workerName</c> among the workers the user manages.
    /// </summary>
    public static Task<(StaffMember Worker, string Error)> FindWorkerAsync(IServiceProvider services, ClaimsPrincipal user, AIFunctionArguments arguments, CancellationToken cancellationToken)
        => FindWorkerAsync(services, user, arguments, requireScope: true, cancellationToken);

    /// <summary>
    /// Finds the worker named by <c>workerId</c> or <c>workerName</c>, checking the user manages them only when
    /// <paramref name="requireScope"/> is set.
    /// </summary>
    public static async Task<(StaffMember Worker, string Error)> FindWorkerAsync(IServiceProvider services, ClaimsPrincipal user, AIFunctionArguments arguments, bool requireScope, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IStaffMemberManager>();
        var scopeService = services.GetRequiredService<IWorkforceScopeService>();
        var id = Read(arguments, "workerId");
        var name = Read(arguments, "workerName");
        StaffMember worker;

        if (id is not null)
        {
            worker = await manager.FindByIdAsync(id, cancellationToken);

            if (worker is null)
            {
                return (null, $"No worker has the ID '{id}'.");
            }
        }
        else if (name is not null)
        {
            var (_, found) = await manager.SearchAsync(new StaffQuery { Search = name }, 0, 20, cancellationToken);
            var candidates = new List<StaffMember>();

            foreach (var item in found)
            {
                if (!requireScope || await scopeService.ManagesWorkerAsync(user, item, cancellationToken))
                {
                    candidates.Add(item);
                }
            }

            var exact = candidates.Where(item => WorkforceNames.Normalize(item.DisplayName) == WorkforceNames.Normalize(name)).ToList();
            var matches = exact.Count > 0 ? exact : candidates;

            if (matches.Count == 0)
            {
                return (null, $"No worker called '{name}' was found among the workers the user manages.");
            }

            if (matches.Count > 1)
            {
                return (null, $"Several workers match '{name}': {string.Join(", ", matches.Take(10).Select(item => $"{item.DisplayName} ({item.ItemId})"))}. Give the workerId.");
            }

            return (matches[0], null);
        }
        else
        {
            return (null, "Give the worker's ID (workerId) or name (workerName).");
        }

        if (requireScope && !await scopeService.ManagesWorkerAsync(user, worker, cancellationToken))
        {
            return (null, $"The user doesn't manage {worker.DisplayName}.");
        }

        return (worker, null);
    }

    /// <summary>
    /// Gets the path of a Workforce admin page, or <see langword="null"/> when there is no request to build it from.
    /// </summary>
    public static string GetAdminPath(IServiceProvider services, string controller, string action, object values = null)
    {
        var httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services?.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = ModuleArea,
            ["controller"] = controller,
            ["action"] = action,
        };

        return linkGenerator.GetPathByRouteValues(httpContext, null, routeValues);
    }
}
