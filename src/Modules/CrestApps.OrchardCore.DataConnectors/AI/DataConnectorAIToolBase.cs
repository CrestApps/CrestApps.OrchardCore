using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// A data connector AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or lacks the
/// permission the matching admin page asks for, and it never takes a credential (an API key, password, bearer token,
/// client secret or header) as an argument, because a credential is entered on the admin page, never in a chat.
/// </summary>
public abstract class DataConnectorAIToolBase : AIFunction
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

    /// <summary>
    /// The argument names a tool refuses at any depth, in any letter case, because they carry a credential: the secrets a
    /// connection stores, and headers, whose values often carry one. Names are matched whole, so settings such as
    /// <c>apiKeyName</c>, <c>apiKeyPrefix</c> or <c>tokenUrl</c> are accepted.
    /// </summary>
    public static readonly IReadOnlySet<string> CredentialArgumentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "apiKey",
        "password",
        "bearerToken",
        "clientSecret",
        "token",
        "accessToken",
        "secret",
        "secrets",
        "connectionString",
        "headers",
    };

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

    /// <summary>
    /// Gets the permission the caller needs, the one the matching admin page asks for.
    /// </summary>
    protected abstract Permission RequiredPermission { get; }

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;
        var user = GetCaller(services);

        if (user is null || !await IsAuthorizedAsync(services, user, RequiredPermission))
        {
            return NotAllowedMessage;
        }

        var credential = FindCredentialArgument(arguments);

        if (credential is not null)
        {
            return $"Refused: '{credential}' carries a credential. A tool never takes an API key, password, token, client secret or header value; a person enters it on the connection's page: {GetAdminPath(services, "DataConnections", "Index")}";
        }

        return await InvokeAsync(arguments, services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who holds <see cref="RequiredPermission"/>.
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
    /// Whether the caller holds a permission.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <returns><see langword="true"/> when the caller holds it.</returns>
    protected static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal user, Permission permission)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return user is not null && authorizationService is not null && await authorizationService.AuthorizeAsync(user, permission);
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
        var node = ReadNode(arguments, name);
        var text = node is JsonValue value && value.TryGetValue<string>(out var raw) ? raw : node?.ToJsonString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Whether an argument was given, even empty.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns><see langword="true"/> when the argument is present.</returns>
    protected static bool Has(AIFunctionArguments arguments, string name)
        => arguments.TryGetValue(name, out _);

    /// <summary>
    /// Reads an optional whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The number, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a whole number.</returns>
    protected static bool TryReadInt(AIFunctionArguments arguments, string name, out int? value)
    {
        value = null;

        var node = ReadNode(arguments, name);

        if (node is null)
        {
            return true;
        }

        if (node is JsonValue number && number.TryGetValue<int>(out var parsed))
        {
            value = parsed;

            return true;
        }

        if (node is JsonValue number64 && number64.TryGetValue<double>(out var real) && real == Math.Floor(real) && real is >= int.MinValue and <= int.MaxValue)
        {
            value = (int)real;

            return true;
        }

        if (node is JsonValue text && text.TryGetValue<string>(out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            value = parsed;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads an optional true or false argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The value, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when the argument is given but is not true or false.</returns>
    protected static bool TryReadBool(AIFunctionArguments arguments, string name, out bool? value)
    {
        value = null;

        var node = ReadNode(arguments, name);

        if (node is null)
        {
            return true;
        }

        if (node is JsonValue flag && flag.TryGetValue<bool>(out var parsed))
        {
            value = parsed;

            return true;
        }

        if (node is JsonValue text && text.TryGetValue<string>(out var raw) && bool.TryParse(raw, out parsed))
        {
            value = parsed;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads an optional enum argument by name, in any letter case.
    /// </summary>
    /// <typeparam name="TEnum">The enum.</typeparam>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The value, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when the argument is given but names no member.</returns>
    protected static bool TryReadEnum<TEnum>(AIFunctionArguments arguments, string name, out TEnum? value)
        where TEnum : struct, Enum
    {
        value = null;

        var text = ReadString(arguments, name);

        if (text is null)
        {
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed))
        {
            value = parsed;

            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads an argument that holds a JSON array.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <param name="value">The array, or <see langword="null"/> when it is missing.</param>
    /// <returns><see langword="false"/> when the argument is given but is not an array.</returns>
    protected static bool TryReadArray(AIFunctionArguments arguments, string name, out JsonArray value)
    {
        value = null;

        var node = ReadNode(arguments, name);

        if (node is null)
        {
            return true;
        }

        if (node is JsonValue text && text.TryGetValue<string>(out var raw) && raw.TrimStart().StartsWith('['))
        {
            try
            {
                node = JsonNode.Parse(raw);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        value = node as JsonArray;

        return value is not null;
    }

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
    {
        TryReadInt(arguments, "page", out var page);
        TryReadInt(arguments, "pageSize", out var pageSize);

        return (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));
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
    /// Gets the path of a Data Connectors admin page, for a person to finish what a tool may not do.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="controller">The controller: <c>DataConnections</c> or <c>ConnectedDataSets</c>.</param>
    /// <param name="action">The action.</param>
    /// <param name="id">The record's ID, or <see langword="null"/>.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetAdminPath(IServiceProvider services, string controller, string action, string id = null)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var values = new RouteValueDictionary
        {
            ["area"] = DataConnectorConstants.Features.Area,
            ["controller"] = controller,
            ["action"] = action,
        };

        if (!string.IsNullOrEmpty(id))
        {
            values["id"] = id;
        }

        return linkGenerator.GetPathByRouteValues(httpContext, null, values);
    }

    /// <summary>
    /// Finds an argument whose name is one of <see cref="CredentialArgumentNames"/>, at any depth.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The name of the first one, or <see langword="null"/>.</returns>
    internal static string FindCredentialArgument(AIFunctionArguments arguments)
    {
        foreach (var (name, value) in arguments)
        {
            if (CredentialArgumentNames.Contains(name))
            {
                return name;
            }

            var found = FindCredential(ToNode(value));

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static string FindCredential(JsonNode node)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var (name, value) in json)
                {
                    if (CredentialArgumentNames.Contains(name))
                    {
                        return name;
                    }

                    var found = FindCredential(value);

                    if (found is not null)
                    {
                        return found;
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    var found = FindCredential(item);

                    if (found is not null)
                    {
                        return found;
                    }
                }

                break;
        }

        return null;
    }

    private static JsonNode ReadNode(AIFunctionArguments arguments, string name)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.TryGetValue(name, out var value) ? ToNode(value) : null;
    }

    private static JsonNode ToNode(object value)
    {
        try
        {
            return value switch
            {
                null => null,
                JsonNode node => node.DeepClone(),
                JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
                _ => JsonSerializer.SerializeToNode(value),
            };
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
