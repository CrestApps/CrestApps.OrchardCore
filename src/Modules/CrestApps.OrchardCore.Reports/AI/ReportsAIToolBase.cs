using System.Collections;
using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// A Reports AI tool. It acts as the person using the AI: it refuses a caller who is not signed in, and each tool checks
/// the permission the matching admin action asks for, with the report or view as the resource where the action passes
/// one.
/// </summary>
public abstract class ReportsAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    /// <summary>
    /// The page size a list returns when none is asked for.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The largest page size a list returns.
    /// </summary>
    public const int MaxPageSize = 50;

    // The result is read by the model, not put in a page, so names in any language stay as written instead of becoming
    // \uXXXX escapes.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var user = GetCaller(arguments.Services);

        if (user is null)
        {
            return NotAllowedMessage;
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller. The tool checks its own permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller of the tool. The chat hubs publish the caller through <see cref="IUserAccessor"/> for the
    /// whole invocation, which also works on transports where the request is not available to the hub method; the
    /// request's user is the fallback.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    protected static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Whether the caller holds a permission, on a resource when one is given, as the admin actions check it.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="resource">The report or view the permission is checked on, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the caller holds it.</returns>
    protected static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal user, Permission permission, object resource = null)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return user is not null && permission is not null && authorizationService is not null && (resource is null
            ? await authorizationService.AuthorizeAsync(user, permission)
            : await authorizationService.AuthorizeAsync(user, permission, resource));
    }

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result, built from an explicit list of fields.</param>
    /// <returns>The JSON text.</returns>
    protected static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    protected static string ReadString(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.ToString(),
            }
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The number, or <see langword="null"/> when it is missing or not a whole number.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => int.TryParse(ReadString(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadString(arguments, name), out var value) ? value : null;

    /// <summary>
    /// Reads whether an argument was given at all, even empty.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns><see langword="true"/> when it was given with a value.</returns>
    protected static bool Has(AIFunctionArguments arguments, string name)
        => arguments is not null && arguments.TryGetValue(name, out var value) && value is not null &&
            !(value is JsonElement element && element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);

    /// <summary>
    /// Reads an argument as JSON, whatever form the model or a caller gave it in.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The JSON value, or <see langword="null"/> when it is missing.</returns>
    protected static JsonElement? ReadJson(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var value = arguments[name];

        if (value is JsonElement element)
        {
            // A model sometimes sends an object as JSON text.
            if (element.ValueKind == JsonValueKind.String)
            {
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(element.GetString());
                }
                catch (JsonException)
                {
                    return element;
                }
            }

            return element;
        }

        if (value is string text)
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(text);
            }
            catch (JsonException)
            {
                return JsonSerializer.SerializeToElement(text);
            }
        }

        return JsonSerializer.SerializeToElement(value, value.GetType());
    }

    /// <summary>
    /// Reads a list of texts.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed, non-empty texts, or <see langword="null"/> when the argument is missing.</returns>
    protected static IList<string> ReadStringList(AIFunctionArguments arguments, string name)
    {
        if (!Has(arguments, name))
        {
            return null;
        }

        var value = arguments[name];

        if (value is not string && value is IEnumerable enumerable)
        {
            return enumerable.Cast<object>()
                .Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)?.Trim())
                .Where(item => !string.IsNullOrEmpty(item))
                .ToList();
        }

        var json = ReadJson(arguments, name);

        if (json is not { } element)
        {
            return null;
        }

        return ToStrings(element);
    }

    /// <summary>
    /// Reads the values of filters: an object whose keys are filter ids and whose values are a text or a list of texts.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The values by key, or <see langword="null"/> when the argument is missing.</returns>
    protected static Dictionary<string, IList<string>> ReadValuesByKey(AIFunctionArguments arguments, string name)
    {
        if (ReadJson(arguments, name) is not { ValueKind: JsonValueKind.Object } element)
        {
            return null;
        }

        var values = new Dictionary<string, IList<string>>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            values[property.Name.Trim()] = ToStrings(property.Value);
        }

        return values;
    }

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

    /// <summary>
    /// Reads a day written as <c>yyyy-MM-dd</c>.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="day">The day, or <see langword="null"/> when the argument is missing.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a day.</returns>
    protected static bool TryReadDay(AIFunctionArguments arguments, string name, out DateOnly? day)
    {
        day = null;

        var text = ReadString(arguments, name);

        if (text is null)
        {
            return true;
        }

        // A model sometimes adds a time; only the day counts.
        if (text.Length > 10 && text[10] is 'T' or ' ')
        {
            text = text[..10];
        }

        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        day = parsed;

        return true;
    }

    /// <summary>
    /// Reads the period and filter values a report runs or is exported with.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="run">The period and filter values.</param>
    /// <returns>The problem to tell the model, or <see langword="null"/>.</returns>
    internal static string ReadRunArguments(AIFunctionArguments arguments, out ReportRunArguments run)
    {
        run = null;

        if (!TryReadDay(arguments, "from", out var from) || !TryReadDay(arguments, "to", out var to))
        {
            return "Give from and to as days written yyyy-MM-dd, such as 2026-01-31.";
        }

        run = new ReportRunArguments
        {
            From = from,
            To = to,
            AllTime = ReadBool(arguments, "allTime") == true,
            Filters = ReadValuesByKey(arguments, "filters"),
        };

        return null;
    }

    /// <summary>
    /// Converts a stored UTC time to the user's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static async Task<string> ToLocalAsync(IServiceProvider services, DateTime? utc)
    {
        var localClock = services.GetService<ILocalClock>();

        if (utc is null || utc.Value == default || localClock is null)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes a time that is already in the user's or the site's time zone, as report rows hold them.
    /// </summary>
    /// <param name="local">The local time.</param>
    /// <returns>The time without an offset, or the day alone at midnight.</returns>
    protected static string FormatLocal(DateTime local)
        => local.TimeOfDay == TimeSpan.Zero
            ? local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : local.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the path of a named admin route of the module, such as a report page or an export.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="routeName">The route name.</param>
    /// <param name="values">The route values, such as the report's id.</param>
    /// <param name="query">Values added to the query string, in order; a name may repeat.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetRoutePath(IServiceProvider services, string routeName, object values = null, IEnumerable<KeyValuePair<string, string>> query = null)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var path = linkGenerator.GetPathByRouteValues(httpContext, routeName, values);

        if (path is null)
        {
            return null;
        }

        var queryString = QueryString.Create(query ?? []);

        return path + queryString.ToUriComponent();
    }

    private static List<string> ToStrings(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Array => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                .Select(item => item?.Trim())
                .Where(item => !string.IsNullOrEmpty(item))
                .ToList(),
            JsonValueKind.Null or JsonValueKind.Undefined => [],
            JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()) ? [] : [element.GetString().Trim()],
            _ => [element.ToString()],
        };
}
