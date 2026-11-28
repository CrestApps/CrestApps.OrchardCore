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
using YesSql;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Marketing.AI;

/// <summary>
/// A marketing AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or who lacks
/// the tool's <see cref="Permission"/>, the permission of the admin page it mirrors, before it reads an argument, and
/// refuses any argument its schema does not declare.
/// </summary>
internal abstract class MarketingAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    /// <summary>
    /// The default number of entries a list returns.
    /// </summary>
    protected const int DefaultPageSize = 20;

    /// <summary>
    /// The most entries a list returns.
    /// </summary>
    protected const int MaxPageSize = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private HashSet<string> _argumentNames;

    /// <summary>
    /// Gets the permission the caller needs: Manage marketing, which the Segments and Campaigns pages check.
    /// </summary>
    protected virtual Permission Permission => MarketingPermissions.ManageMarketing;

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

        var services = arguments.Services;
        var caller = GetCaller(services);
        var authorizationService = services.GetService<IAuthorizationService>();

        if (caller is null || authorizationService is null || !await authorizationService.AuthorizeAsync(caller, Permission))
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, caller, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who has the permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller: the user the chat hubs publish through <see cref="IUserAccessor"/>, or the request's user.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    protected static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        var user = services.GetService<IUserAccessor>()?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        }

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Gets the caller's user id, as the admin pages record who acted.
    /// </summary>
    /// <param name="caller">The caller.</param>
    /// <returns>The user id, or the user name when the id claim is missing.</returns>
    protected static string GetUserId(ClaimsPrincipal caller)
        => caller?.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller?.Identity?.Name;

    /// <summary>
    /// Commits the session's unit of work at once, as the admin page does at the end of its request. When someone else
    /// changed the same record at the same moment, nothing is kept and the answer says so.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when it was saved, or the sentence that says why not.</returns>
    protected static async Task<string> CommitAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var session = services.GetService<YesSqlSession>();

        if (session is null)
        {
            return null;
        }

        try
        {
            await session.SaveChangesAsync(cancellationToken);

            return null;
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            return "Nothing was saved: the record changed at the same moment. Read it again and retry in your next message.";
        }
    }

    /// <summary>
    /// Gets the path of an admin page by its route name.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="routeName">The name of the admin route.</param>
    /// <param name="values">The route values, with the module's <c>area</c>.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetAdminPath(IServiceProvider services, string routeName, object values)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        try
        {
            return linkGenerator.GetPathByRouteValues(httpContext, routeName, new RouteValueDictionary(values));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a UTC time in the caller's or the site's time zone.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="utc">The UTC time, or <see langword="null"/>.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    protected static async Task<string> LocalTimeAsync(IServiceProvider services, DateTime? utc)
    {
        if (utc is null || utc.Value == default)
        {
            return null;
        }

        var localClock = services.GetService<ILocalClock>();

        if (localClock is null)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes an amount for a sentence.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <returns>The amount and currency, such as 20.00 USD.</returns>
    protected static string Format(decimal amount, string currency)
        => $"{amount.ToString("0.00##", CultureInfo.InvariantCulture)} {currency}";

    /// <summary>
    /// Writes an enum value as the tools name it, starting in lower case.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name, such as <c>pastDue</c>.</returns>
    protected static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    /// <summary>
    /// Builds a list result: the page's items, the total, the paging and whether more entries exist.
    /// </summary>
    /// <param name="items">The page's items, each built from an explicit list of fields.</param>
    /// <param name="total">How many entries match.</param>
    /// <param name="page">The page, from 1.</param>
    /// <param name="pageSize">The page size.</param>
    /// <returns>The JSON text.</returns>
    protected static string ListResult(IEnumerable<object> items, int total, int page, int pageSize)
    {
        var list = items.ToList();

        return Json(new
        {
            items = list,
            total,
            page,
            pageSize,
            truncated = (page - 1) * pageSize + list.Count < total,
        });
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
    /// Reads an argument as JSON.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or null.</returns>
    protected static JsonElement? ReadJson(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element;
    }

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    protected static string ReadString(AIFunctionArguments arguments, string name)
    {
        if (ReadJson(arguments, name) is not { } element)
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The number, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when it is given but is not a whole number.</returns>
    protected static bool TryReadInt(AIFunctionArguments arguments, string name, out int? value)
    {
        value = null;

        if (ReadJson(arguments, name) is not { } element)
        {
            return true;
        }

        if ((element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var number)) ||
            (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)))
        {
            value = number;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads a decimal number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The number, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when it is given but is not a number.</returns>
    protected static bool TryReadDecimal(AIFunctionArguments arguments, string name, out decimal? value)
    {
        value = null;

        if (ReadJson(arguments, name) is not { } element)
        {
            return true;
        }

        if ((element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var number)) ||
            (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)))
        {
            value = number;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing or not true or false.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => ReadJson(arguments, name) is { } element
            ? element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var parsed) => parsed,
                _ => null,
            }
            : null;

    /// <summary>
    /// Reads an enum argument by its name, ignoring case.
    /// </summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The value, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when it is given but names no value of the enum.</returns>
    protected static bool TryReadEnum<TEnum>(AIFunctionArguments arguments, string name, out TEnum? value)
        where TEnum : struct, Enum
    {
        value = null;

        if (ReadString(arguments, name) is not { } text)
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
    /// Reads the page and page size arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
    {
        TryReadInt(arguments, "page", out var page);
        TryReadInt(arguments, "pageSize", out var pageSize);

        return (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
    }

    private static HashSet<string> GetArgumentNames(JsonElement schema)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (schema.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }

        return names;
    }
}
