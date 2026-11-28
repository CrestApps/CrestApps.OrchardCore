using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.AI;

/// <summary>
/// Argument reading, paging, JSON, times, scope and lookups for the contact center workforce AI tools.
/// </summary>
internal static class ContactCenterWorkforceTools
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    public static string ToJson(object value)
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
    /// Reads a time of day (HH:mm).
    /// </summary>
    public static TimeOnly? ReadTime(AIFunctionArguments arguments, string name)
        => TimeOnly.TryParseExact(Read(arguments, name), ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    /// <summary>
    /// Reads an enum value by name, ignoring case; numbers are refused, so a stray number never picks a value by position.
    /// </summary>
    public static bool TryParseEnum<TEnum>(string text, out TEnum value)
        where TEnum : struct, Enum
        => Enum.TryParse(text, ignoreCase: true, out value) && Enum.IsDefined(value) && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

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

    /// <summary>
    /// Formats a stored UTC time in a time zone the admin page shows it in (a worker's, or a planning group's team's).
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
    /// Formats a stored UTC time as a time of day in a time zone.
    /// </summary>
    public static string TimeInZone(DateTime utc, string timeZoneId)
        => CalendarTime.ToLocal(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZoneId).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a stored UTC time in the user's or the site's time zone.
    /// </summary>
    public static async Task<string> LocalAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var local = await services.GetRequiredService<ILocalClock>().ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc)));

        return local.ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets today's date in a time zone.
    /// </summary>
    public static DateOnly Today(IServiceProvider services, string timeZoneId)
        => DateOnly.FromDateTime(CalendarTime.ToLocal(services.GetRequiredService<IClock>().UtcNow, timeZoneId));

    /// <summary>
    /// Rounds a ratio to a percentage with one decimal.
    /// </summary>
    public static double? Percent(double? ratio)
        => ratio is null ? null : Math.Round(ratio.Value * 100, 1);

    public static double Minutes(double seconds)
        => Math.Round(seconds / 60, 1);

    /// <summary>
    /// Finds a team by its ID or name among every team.
    /// </summary>
    public static async Task<Team> FindTeamAsync(IServiceProvider services, string idOrName, CancellationToken cancellationToken)
    {
        return (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken))
            .Select(entry => entry.Team)
            .FirstOrDefault(team => team.ItemId == idOrName || WorkforceNames.Normalize(team.Name) == WorkforceNames.Normalize(idOrName));
    }

    /// <summary>
    /// Reads the team filter (<c>teamName</c>, a team's name or ID) and returns the teams to look in, as the admin lists
    /// do: the named team and its sub-teams when the user manages it, otherwise every team the user manages
    /// (<see langword="null"/> when they manage all).
    /// </summary>
    public static async Task<(IReadOnlyCollection<string> TeamIds, string Error)> TeamFilterAsync(IServiceProvider services, ClaimsPrincipal user, AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        if (!scope.All && scope.TeamIds.Count == 0)
        {
            return (null, "The user doesn't manage any team, so there is nothing to show.");
        }

        var teamName = Read(arguments, "teamName");

        if (teamName is null)
        {
            return (scope.All ? null : scope.TeamIds.ToArray(), null);
        }

        var team = await FindTeamAsync(services, teamName, cancellationToken);

        if (team is null)
        {
            return (null, $"No team is called '{teamName}'.");
        }

        if (!scope.Covers(team.ItemId))
        {
            return (null, $"The user doesn't manage {team.Name}.");
        }

        var subtree = await services.GetRequiredService<ITeamManager>().GetSubtreeIdsAsync(team.ItemId, cancellationToken);

        return (subtree.Append(team.ItemId).Distinct(StringComparer.Ordinal).ToArray(), null);
    }

    /// <summary>
    /// Finds the worker named by <c>workerId</c> or <c>workerName</c> among the workers the user manages.
    /// </summary>
    public static async Task<(StaffMember Worker, string Error)> FindWorkerAsync(IServiceProvider services, ClaimsPrincipal user, AIFunctionArguments arguments, CancellationToken cancellationToken)
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
                if (await scopeService.ManagesWorkerAsync(user, item, cancellationToken))
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

        if (!await scopeService.ManagesWorkerAsync(user, worker, cancellationToken))
        {
            return (null, $"The user doesn't manage {worker.DisplayName}.");
        }

        return (worker, null);
    }

    /// <summary>
    /// Finds the planning group named by <c>planningGroupName</c> (a name or an ID). With no name, the only planning group
    /// is taken; with several, the names are returned to choose from.
    /// </summary>
    public static async Task<(PlanningGroup Group, string Error)> FindPlanningGroupAsync(IServiceProvider services, AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var groups = (await services.GetRequiredService<PlanningGroupManager>().GetAllAsync(cancellationToken))
            .OrderBy(group => group.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (groups.Count == 0)
        {
            return (null, "No planning group exists yet. Planning groups are set up under Workforce > Forecasting > Planning Groups.");
        }

        var name = Read(arguments, "planningGroupName");

        if (name is null)
        {
            if (groups.Count == 1)
            {
                return (groups[0], null);
            }

            return (null, $"Name the planning group (planningGroupName): {string.Join(", ", groups.Select(group => group.Name))}.");
        }

        var match = groups.FirstOrDefault(group => group.ItemId == name || WorkforceNames.Normalize(group.Name) == WorkforceNames.Normalize(name));

        if (match is null)
        {
            return (null, $"No planning group is called '{name}'. The planning groups are: {string.Join(", ", groups.Select(group => group.Name))}.");
        }

        return (match, null);
    }

    /// <summary>
    /// Gets the names of teams by their IDs.
    /// </summary>
    public static async Task<Dictionary<string, string>> TeamNamesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken))
            .ToDictionary(entry => entry.Team.ItemId, entry => entry.Team.Name, StringComparer.Ordinal);
}
