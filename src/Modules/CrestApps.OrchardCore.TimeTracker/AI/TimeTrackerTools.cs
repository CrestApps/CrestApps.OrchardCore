using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;
using YesSqlSession = global::YesSql.ISession;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// Argument reading, paging, JSON, times and look-ups shared by the Time Tracker AI tools. Times are shown in the
/// Time Tracker's time zone (the site's), as the Time Tracker pages show them and count days.
/// </summary>
internal static class TimeTrackerTools
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    private static readonly string[] _dateTimeFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"];

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
    /// Reads a local date and time (yyyy-MM-ddTHH:mm) in the Time Tracker's time zone and returns it in UTC.
    /// </summary>
    public static DateTime? ReadLocalAsUtc(AIFunctionArguments arguments, string name, string zone)
    {
        var text = Read(arguments, name);

        if (text is null || !DateTime.TryParseExact(text, _dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        return TimeTrackerTimeZone.ToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone);
    }

    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Builds a page of a list result.
    /// </summary>
    public static object Paged<T>(IReadOnlyList<T> items, int total, int page, int pageSize, string timeZone)
        => new
        {
            items,
            total,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < total,
            timeZone,
        };

    /// <summary>
    /// Formats a stored UTC time in the Time Tracker's time zone.
    /// </summary>
    public static string Local(DateTime? utc, string zone)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        return TimeTrackerTimeZone.ToLocal(utc.Value, zone).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
    }

    public static decimal Hours(decimal minutes)
        => Math.Round(minutes / 60m, 2);

    /// <summary>
    /// Gets the stages, ordered as the pages list them.
    /// </summary>
    public static async Task<List<TimeStage>> StagesAsync(IServiceProvider services, CancellationToken cancellationToken)
        => [.. (await services.GetRequiredService<TimeStageStore>().GetAllAsync(cancellationToken))
            .OrderBy(stage => stage.Position)
            .ThenBy(stage => stage.Name, StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>
    /// Finds a stage by its ID or name.
    /// </summary>
    public static TimeStage FindStage(IEnumerable<TimeStage> stages, string idOrName)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            return null;
        }

        var all = stages.ToList();

        return all.FirstOrDefault(stage => stage.ItemId == idOrName)
            ?? all.FirstOrDefault(stage => string.Equals(stage.Name?.Trim(), idOrName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? all.FirstOrDefault(stage => string.Equals(stage.Code?.Trim(), idOrName.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds the clock that keeps a person's time, as My Time does, or <see langword="null"/> when Time Tracker keeps it.
    /// </summary>
    public static async Task<ExternalTimeClockInfo> ExternalClockAsync(IServiceProvider services, string userId, CancellationToken cancellationToken)
    {
        foreach (var clock in services.GetServices<IExternalTimeClock>())
        {
            if (await clock.FindAsync(userId, cancellationToken) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the clock that keeps an entry, as the Time Entries page does, or <see langword="null"/>.
    /// </summary>
    public static async Task<ExternalTimeClockInfo> KeptByAsync(IServiceProvider services, TimeEntry entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(entry.ManagedBy))
        {
            return null;
        }

        foreach (var clock in services.GetServices<IExternalTimeClock>())
        {
            if (await clock.FindByIdAsync(entry.ManagedBy, cancellationToken) is { } found)
            {
                return found;
            }
        }

        return new ExternalTimeClockInfo { Id = entry.ManagedBy, Name = entry.ManagedBy };
    }

    /// <summary>
    /// Gets the name a person is known by, as the Time Tracker pages record it.
    /// </summary>
    public static async Task<string> DisplayNameAsync(IServiceProvider services, IUser user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return null;
        }

        var names = services.GetService<IDisplayNameProvider>();

        return (names is null ? null : await names.GetAsync(user, cancellationToken)) ?? user.UserName;
    }

    /// <summary>
    /// Gets the signed-in person's name, as My Time records it.
    /// </summary>
    public static async Task<string> CallerNameAsync(IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userManager = services.GetService<UserManager<IUser>>();
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var found = userManager is null || string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);

        return found is null
            ? user.Identity?.Name
            : await DisplayNameAsync(services, found, cancellationToken) ?? user.Identity?.Name;
    }

    /// <summary>
    /// Saves the session at once, so a later call in the same chat reads the change.
    /// </summary>
    public static Task SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
        => services.GetService<YesSqlSession>()?.SaveChangesAsync(cancellationToken) ?? Task.CompletedTask;

    /// <summary>
    /// Describes an entry with an explicit list of fields.
    /// </summary>
    public static async Task<object> EntryAsync(IServiceProvider services, TimeEntry entry, IReadOnlyDictionary<string, TimeStage> stages, string zone, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var end = entry.EndUtc ?? nowUtc;
        var keptBy = await KeptByAsync(services, entry, cancellationToken);

        return new
        {
            id = entry.ItemId,
            userId = entry.UserId,
            user = entry.UserName,
            stageId = entry.StageId,
            stage = stages.GetValueOrDefault(entry.StageId ?? string.Empty)?.Name,
            start = Local(entry.StartUtc, zone),
            end = Local(entry.EndUtc, zone),
            open = entry.IsOpen,
            minutes = (int)Math.Max(0, Math.Round((end - entry.StartUtc).TotalMinutes)),
            note = entry.Note,
            source = entry.Source,
            keptBy = keptBy?.Name,
            changes = entry.Changes
                .OrderByDescending(change => change.Utc)
                .Take(5)
                .Select(change => new
                {
                    at = Local(change.Utc, zone),
                    by = change.By,
                    reason = change.Reason,
                    before = change.Before,
                })
                .ToList(),
            changeCount = entry.Changes.Count,
        };
    }
}
