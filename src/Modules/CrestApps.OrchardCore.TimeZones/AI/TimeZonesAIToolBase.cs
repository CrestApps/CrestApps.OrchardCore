using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.TimeZones.AI;

/// <summary>
/// A time zone map AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or lacks
/// <c>ManageTimeZoneMaps</c>, the permission of every Time Zones admin page, before it reads any argument, and refuses an
/// argument its schema does not declare.
/// </summary>
internal abstract class TimeZonesAIToolBase : AIFunction
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

        if (caller is null || authorizationService is null || !await authorizationService.AuthorizeAsync(caller, TimeZonesConstants.Permissions.ManageTimeZoneMaps))
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was done: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a caller who may manage time zone maps.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller: the user the chat hubs publish through <see cref="IUserAccessor"/>, or the request's user.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    protected static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
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
