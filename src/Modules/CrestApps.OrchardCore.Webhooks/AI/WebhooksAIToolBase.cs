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

namespace CrestApps.OrchardCore.Webhooks.AI;

/// <summary>
/// A webhooks AI tool. It acts as the person using the AI: it refuses a caller who is not signed in or lacks
/// <see cref="WebhookPermissions.ManageWebhooks"/> (named <c>ManageCommerceWebhooks</c>), which every action of the
/// webhooks admin page checks, and it never takes a signing secret: a secret is created and shown on the webhook's
/// admin page only.
/// </summary>
public abstract class WebhooksAIToolBase : AIFunction
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
    /// The argument names refused in any letter case, because they would carry a signing secret.
    /// </summary>
    internal static readonly HashSet<string> SecretArgumentNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret",
        "signingSecret",
        "protectedSecret",
        "webhookSecret",
    };

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

        var services = arguments.Services;
        var user = GetCaller(services);

        if (user is null || !await IsAuthorizedAsync(services, user))
        {
            return NotAllowedMessage;
        }

        if (arguments.Keys.FirstOrDefault(SecretArgumentNames.Contains) is { } secretArgument)
        {
            return $"Refused: '{secretArgument}' would carry a signing secret. A tool never takes or returns a webhook's signing secret; a person copies it from the webhook's admin page, and rotateWebhookSecret gives a webhook a new one.";
        }

        return await InvokeAsync(arguments, services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller who may manage webhooks.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the signed-in caller of the tool: the user the chat hubs publish through <see cref="IUserAccessor"/>, or the
    /// request's user.
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
    /// Reads a list of texts, given as an array or as one text separated by commas.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The trimmed, non-empty texts, or <see langword="null"/> when the argument is missing.</returns>
    protected static List<string> ReadStringList(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        IEnumerable<string> items = value switch
        {
            JsonElement { ValueKind: JsonValueKind.Array } element => element.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString()),
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
            JsonElement element => (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()).Split(','),
            string text => text.Split(','),
            System.Collections.IEnumerable enumerable => enumerable.Cast<object>().Select(item => Convert.ToString(item, CultureInfo.InvariantCulture)),
            _ => [Convert.ToString(value, CultureInfo.InvariantCulture)],
        };

        return items?
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrEmpty(item))
            .ToList();
    }

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    protected static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
        => (Math.Max(1, ReadInt(arguments, "page") ?? 1), Math.Clamp(ReadInt(arguments, "pageSize") ?? DefaultPageSize, 1, MaxPageSize));

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
    /// Gets the path of a webhook's admin page, where its signing secret is shown and rotated.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="webhookId">The webhook's id.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    protected static string GetEditPath(IServiceProvider services, string webhookId)
    {
        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        return linkGenerator.GetPathByRouteValues(httpContext, "WebhooksEdit", new { area = WebhooksConstants.Feature.Webhooks, itemId = webhookId });
    }

    private static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal user)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(user, WebhookPermissions.ManageWebhooks);
    }
}
