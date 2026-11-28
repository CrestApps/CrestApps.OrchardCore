using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Agreements.Core.Services;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Argument reading, paging, JSON, local times, admin links, lookups and saving for the work order and service agreement
/// AI tools.
/// </summary>
internal static class WorkOrderToolSupport
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    public const string ModuleArea = "CrestApps.OrchardCore.WorkOrders";

    public const string ServiceLevelsArea = ServiceLevelsConstants.Features.ServiceLevels;

    public const string WorkOrderNotFound = "No work order has that number or ID. Use listWorkOrders to find its number.";

    public const string AgreementNotFound = "No service agreement has that number or ID. Use listRecords with the kind agreement to find it.";

    public const string ConcurrencyRefused = "Someone else changed this record at the same time, so nothing was saved. Try again.";

    private static readonly JsonSerializerOptions _jsonOptions = JsonSerializerOptions.Web;

    public static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    public static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.TryGetValue(name, out var value) && value is not null && value is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>
    /// Reads a text argument, trimmed; empty text is returned as empty, a missing argument as <see langword="null"/>.
    /// </summary>
    public static string ReadRaw(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var value = arguments[name];
        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return text?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Reads a text argument; empty text is <see langword="null"/>.
    /// </summary>
    public static string Read(AIFunctionArguments arguments, string name)
    {
        var text = ReadRaw(arguments, name);

        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadRaw(arguments, name), out var value) ? value : null;

    /// <summary>
    /// Reads a whole number, or <see langword="null"/> when it is missing or not a whole number.
    /// </summary>
    public static int? ReadInt(AIFunctionArguments arguments, string name)
        => ParseInt(ReadRaw(arguments, name));

    /// <summary>
    /// Reads a number, or <see langword="null"/> when it is missing or not a number.
    /// </summary>
    public static decimal? ReadDecimal(AIFunctionArguments arguments, string name)
        => decimal.TryParse(ReadRaw(arguments, name), NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>
    /// Reads a list of texts, given as an array or a comma separated text; <see langword="null"/> when it is missing.
    /// </summary>
    public static IList<string> ReadList(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var element = ToElement(arguments[name]);

        if (element.ValueKind == JsonValueKind.Array)
        {
            return ReadStrings(element);
        }

        return (ReadRaw(arguments, name) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    /// Reads an object argument; <see langword="null"/> when it is missing or not an object.
    /// </summary>
    public static JsonElement? ReadObject(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var element = ToElement(arguments[name]);

        return element.ValueKind == JsonValueKind.Object ? element : null;
    }

    /// <summary>
    /// Reads a list of objects; <see langword="null"/> when it is missing or not a list of objects.
    /// </summary>
    public static IReadOnlyList<JsonElement> ReadObjects(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var element = ToElement(arguments[name]);

        if (element.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = element.EnumerateArray().ToList();

        return items.All(item => item.ValueKind == JsonValueKind.Object) ? items : null;
    }

    public static bool HasProperty(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    /// <summary>
    /// Reads a text property, trimmed; empty text is returned as empty, a missing property as <see langword="null"/>.
    /// </summary>
    public static string GetRaw(JsonElement item, string name)
    {
        if (!HasProperty(item, name))
        {
            return null;
        }

        var value = item.GetProperty(name);

        return (value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString())?.Trim() ?? string.Empty;
    }

    public static string GetText(JsonElement item, string name)
    {
        var text = GetRaw(item, name);

        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static int? GetInt(JsonElement item, string name)
        => ParseInt(GetRaw(item, name));

    public static decimal? GetDecimal(JsonElement item, string name)
        => decimal.TryParse(GetRaw(item, name), NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var number) ? number : null;

    public static bool? GetBool(JsonElement item, string name)
        => bool.TryParse(GetRaw(item, name), out var value) ? value : null;

    /// <summary>
    /// Reads a list of texts property, given as an array or a comma separated text; <see langword="null"/> when missing.
    /// </summary>
    public static IList<string> GetList(JsonElement item, string name)
    {
        if (!HasProperty(item, name))
        {
            return null;
        }

        var value = item.GetProperty(name);

        return value.ValueKind == JsonValueKind.Array
            ? ReadStrings(value)
            : (GetRaw(item, name) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Builds a page of a list result.
    /// </summary>
    public static object Paged<T>(IReadOnlyList<T> items, int total, int page, int pageSize, string timeZoneId = null)
        => new
        {
            items,
            total,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < total,
            timeZoneId,
        };

    /// <summary>
    /// Parses an enum value by name, ignoring case; numbers are refused.
    /// </summary>
    public static bool TryParseEnum<TEnum>(string text, out TEnum value)
        where TEnum : struct, Enum
    {
        value = default;

        return !string.IsNullOrWhiteSpace(text)
            && !char.IsAsciiDigit(text.Trim()[0])
            && Enum.TryParse(text.Trim(), ignoreCase: true, out value)
            && Enum.IsDefined(value);
    }

    public static string Names<TEnum>()
        where TEnum : struct, Enum
        => string.Join(", ", Enum.GetNames<TEnum>());

    /// <summary>
    /// Parses a local date and time written as yyyy-MM-ddTHH:mm (seconds allowed) or a day as yyyy-MM-dd.
    /// </summary>
    public static bool TryParseLocal(string text, out DateTime local)
        => DateTime.TryParseExact(text, ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd"], CultureInfo.InvariantCulture, DateTimeStyles.None, out local);

    /// <summary>
    /// Parses a day written as yyyy-MM-dd.
    /// </summary>
    public static bool TryParseDay(string text, out DateOnly day)
        => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    public static string Day(DateOnly? day)
        => day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the time zone dates and times are shown in: the user's, or the site's.
    /// </summary>
    public static async Task<string> GetTimeZoneIdAsync(IServiceProvider services)
        => (await services.GetRequiredService<ILocalClock>().GetLocalTimeZoneAsync())?.TimeZoneId;

    /// <summary>
    /// Formats a stored UTC time in the user's or the site's time zone, ISO 8601 with its offset.
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
    /// Formats a local date and time, as the work order editor holds it, without converting it.
    /// </summary>
    public static string LocalText(DateTime? local)
        => local?.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Finds a work order by its number (WO-000123, or just 123) or its ID.
    /// </summary>
    public static async Task<WorkOrder> FindWorkOrderAsync(IServiceProvider services, string reference, CancellationToken cancellationToken)
    {
        var text = reference?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var number = text.All(char.IsAsciiDigit) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? WorkOrdersConstants.NumberPrefix + value.ToString("D6", CultureInfo.InvariantCulture)
            : text.ToUpperInvariant();
        var workOrder = await services.GetRequiredService<ISession>()
            .Query<WorkOrder, WorkOrderIndex>(index => index.Number == number, collection: WorkOrdersConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        return workOrder ?? await services.GetRequiredService<WorkOrderService>().FindAsync(text, cancellationToken);
    }

    /// <summary>
    /// Finds a service agreement, a shared agreement, by its number (SA-00012, or just 12) or its ID.
    /// </summary>
    public static async Task<Agreement> FindAgreementAsync(IServiceProvider services, string reference, CancellationToken cancellationToken)
    {
        var text = reference?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // The agreements' own number rule, which every tool that finds an agreement by its number uses.
        return await services.GetRequiredService<AgreementStore>().FindByNumberAsync(text, cancellationToken)
            ?? await services.GetRequiredService<IAgreementManager>().FindByIdAsync(text, cancellationToken);
    }

    /// <summary>
    /// Checks that the user may manage service level policies, as the Service Levels page asks: work order service
    /// levels are shared Service Levels policies. Returns the refusal, or <see langword="null"/> when the user may.
    /// </summary>
    public static async Task<string> AuthorizeServiceLevelsAsync(IServiceProvider services, ClaimsPrincipal user)
        => await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, ServiceLevelsPermissions.ManageServiceLevelPolicies)
            ? null
            : WorkOrderToolBase.NotAllowed(ServiceLevelsPermissions.ManageServiceLevelPolicies);

    /// <summary>
    /// Saves the changes of the call now, as the other field service tools do. Returns a refusal when someone else changed
    /// the same record at the same time.
    /// </summary>
    public static async Task<string> SaveAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        try
        {
            await services.GetRequiredService<ISession>().SaveChangesAsync(cancellationToken);

            return null;
        }
        catch (ConcurrencyException)
        {
            return ConcurrencyRefused;
        }
    }

    /// <summary>
    /// Forgets a record the call changed but did not save, so a later call in the same chat turn reads it as stored.
    /// </summary>
    public static void Forget(IServiceProvider services, object record)
    {
        if (record is not null)
        {
            services.GetRequiredService<ISession>().Detach(record, WorkOrdersConstants.CollectionName);
        }
    }

    /// <summary>
    /// Gets the path of a work orders admin page, or <see langword="null"/> when there is no request to build it from.
    /// </summary>
    public static string GetAdminPath(IServiceProvider services, string controller, string action, object values = null, string area = null)
    {
        var httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services?.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = area ?? ModuleArea,
            ["controller"] = controller,
            ["action"] = action,
        };

        return linkGenerator.GetPathByRouteValues(httpContext, null, routeValues);
    }

    /// <summary>
    /// Gets the path of the editor of a work order settings record: the Work Order Settings page's, or the Service
    /// Levels page's for a service level.
    /// </summary>
    public static string SettingAdminPath(IServiceProvider services, string kind, string id)
        => kind == WorkOrderSettingKinds.ServiceLevels
            ? GetAdminPath(services, "Policies", "Edit", new { id }, ServiceLevelsArea)
            : GetAdminPath(services, "WorkOrderSettings", Controllers.WorkOrderSettingsController.EditActionOf(kind), new { id });

    public static string WorkOrderPath(IServiceProvider services, string id)
        => GetAdminPath(services, "WorkOrders", "Details", new { id });

    public static string AgreementPath(IServiceProvider services, string id)
        => GetAdminPath(services, "ServiceAgreements", "Details", new { id });

    private static List<string> ReadStrings(JsonElement array)
        => array.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();

    private static int? ParseInt(string text)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue
            ? (int)real
            : null;
    }

    private static JsonElement ToElement(object value)
        => value is JsonElement json ? json : JsonSerializer.SerializeToElement(value, _jsonOptions);
}
