using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// A Transactions AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or who lacks
/// the permission of the admin page it stands for, before it reads an argument, and refuses any argument its schema
/// does not declare. Money tools carry an idempotency key, kept a day per site, tool and caller.
/// </summary>
internal abstract class TransactionsAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

    /// <summary>
    /// The longest idempotency key a money tool takes.
    /// </summary>
    protected const int MaxKeyLength = 128;

    private const int DefaultPageSize = 20;

    private const int MaxPageSize = 50;

    private static readonly TimeSpan _keyLifetime = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private HashSet<string> _argumentNames;

    /// <summary>
    /// Gets the permission the caller needs: the one the admin page checks.
    /// </summary>
    protected abstract Permission Permission { get; }

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
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true ? user : null;
    }

    /// <summary>
    /// Runs a money operation once per idempotency key: a repeated key returns the first answer, the same key with other
    /// arguments is refused, and a key in use by a call still running is reported.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="key">The idempotency key, already checked.</param>
    /// <param name="fingerprint">The request's fingerprint.</param>
    /// <param name="noun">What the operation does, for the answers, such as "recorded".</param>
    /// <param name="operation">The operation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The answer.</returns>
    protected async Task<string> RunOnceAsync(
        IServiceProvider services,
        ClaimsPrincipal caller,
        string key,
        string fingerprint,
        string noun,
        Func<CancellationToken, Task<IdempotentOperationAnswer>> operation,
        CancellationToken cancellationToken)
    {
        var cache = services.GetService<IDistributedCache>();
        var locks = services.GetService<IDistributedLock>();

        if (cache is null || locks is null)
        {
            return $"Nothing was {noun}: the site can't keep idempotency keys right now, so this can't be guarded against running twice from a chat. Do it on the admin page.";
        }

        var scope = $"{services.GetService<ShellSettings>()?.Name}|ai-tool:{Name}|{caller.FindFirstValue(ClaimTypes.NameIdentifier) ?? caller.Identity?.Name}";
        var outcome = await IdempotentOperation.RunAsync(cache, locks, scope, key, fingerprint, _keyLifetime, operation, cancellationToken);

        return outcome.Outcome switch
        {
            IdempotentOperationOutcome.Ran => outcome.Value,
            IdempotentOperationOutcome.Replayed => $"Nothing was {noun} again: the idempotency key '{key}' was already used for this. The first call answered: {outcome.Value}",
            IdempotentOperationOutcome.Conflict => $"Nothing was {noun}: the idempotency key '{key}' was already used for something else. Use a new key.",
            _ => $"Nothing was {noun}: a call with the idempotency key '{key}' is still running. Read the record again in your next message.",
        };
    }

    /// <summary>
    /// Reads the idempotency key argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The key, or <see langword="null"/> when it is missing or too long.</returns>
    protected static string ReadKey(AIFunctionArguments arguments)
        => ReadString(arguments, "idempotencyKey") is { Length: <= MaxKeyLength } key ? key : null;

    /// <summary>
    /// Gets the path of a Transactions admin page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="routeName">The name of the admin route.</param>
    /// <param name="values">The route values.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetAdminPath(IServiceProvider services, string routeName, object values)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = TransactionsConstants.Features.Area,
        };

        try
        {
            return linkGenerator.GetPathByRouteValues(httpContext, routeName, routeValues);
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
        if (utc is null || utc.Value == default || services.GetService<ILocalClock>() is not { } localClock)
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
    /// Writes an amount for a fingerprint.
    /// </summary>
    /// <param name="amount">The amount.</param>
    /// <returns>The invariant text.</returns>
    protected static string Invariant(decimal amount)
        => amount.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes an enum value as the tools name it, starting in lower case.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The name.</returns>
    protected static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

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
        if (ReadJson(arguments, name) is not { } element)
        {
            return null;
        }

        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
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
    /// Reads the page and page size arguments, with the page size capped at 50.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
    {
        var page = ReadJson(arguments, "page") is { ValueKind: JsonValueKind.Number } pageValue && pageValue.TryGetInt32(out var number) ? number : 1;
        var pageSize = ReadJson(arguments, "pageSize") is { ValueKind: JsonValueKind.Number } sizeValue && sizeValue.TryGetInt32(out var size) ? size : DefaultPageSize;

        return (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
    }

    private static JsonElement? ReadJson(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var element = value is JsonElement json ? json : JsonSerializer.SerializeToElement(value);

        return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element;
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
