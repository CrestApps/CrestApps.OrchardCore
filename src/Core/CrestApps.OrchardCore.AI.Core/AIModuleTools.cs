using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.AI.Core.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Core;

/// <summary>
/// What the AI tools of the AI knowledge modules (data sources, documents, file sources, web crawlers) share: who is
/// calling, the permission check for that caller, result shapes, paging, local times and the refusal of secret
/// arguments. The tools live in the modules that own each feature; this keeps one copy of the plumbing instead of one per
/// module.
/// </summary>
public static class AIModuleTools
{
    /// <summary>
    /// The page size a list returns when none is asked for.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// The largest page size a list returns.
    /// </summary>
    public const int MaxPageSize = 50;

    // The result is read by the model, not put in a page, so titles in any language stay as written instead of becoming
    // \uXXXX escapes.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Gets the signed-in caller of the tool. The chat hubs publish the caller through <see cref="IUserAccessor"/> for the
    /// whole invocation, which also works on transports where the request is not available to the hub method; the
    /// request's user is the fallback.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The caller, or <see langword="null"/> when nobody is signed in.</returns>
    public static ClaimsPrincipal GetCaller(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var user = services.GetService<IUserAccessor>()?.User
            ?? services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            ? user
            : null;
    }

    /// <summary>
    /// Whether the signed-in caller holds the permission, on the resource when one is given.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="permission">The permission the tool needs.</param>
    /// <param name="resource">The record the permission is checked on, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a signed-in caller holds the permission.</returns>
    public static async Task<bool> IsAuthorizedAsync(IServiceProvider services, Permission permission, object resource = null)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var user = GetCaller(services);

        if (user is null)
        {
            return false;
        }

        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(user, permission, resource);
    }

    /// <summary>
    /// Whether any of the data sources feeds the assistant running the current AI turn, through
    /// <see cref="IAIRunningAssistantGuard"/>. Nothing is protected when no guard is registered.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="dataSourceIds">The IDs of the data sources; blanks are ignored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when one of them is protected.</returns>
    public static async Task<bool> FeedsRunningAssistantAsync(IServiceProvider services, IEnumerable<string> dataSourceIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var guard = services.GetService<IAIRunningAssistantGuard>();

        if (guard is null || dataSourceIds is null)
        {
            return false;
        }

        foreach (var id in dataSourceIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal))
        {
            if (await guard.IsProtectedDataSourceAsync(id, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The refusal a tool returns when a change would alter what the running assistant answers from.
    /// </summary>
    /// <param name="what">What would change, such as "the web crawler 'Docs'".</param>
    /// <param name="path">The admin page where a person can make the change, or <see langword="null"/>.</param>
    /// <returns>The sentence for the model.</returns>
    public static string RunningAssistantRefusal(string what, string path)
        => $"Refused: {what} feeds a data source the assistant running this chat answers from. An assistant cannot change its own knowledge from a chat. A person can make this change on the admin page{(string.IsNullOrEmpty(path) ? "." : $": {path}")}";

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result, built from an explicit list of fields.</param>
    /// <returns>The JSON text.</returns>
    public static string Json(object value)
        => JsonSerializer.Serialize(value, _jsonOptions);

    /// <summary>
    /// Reads the <c>page</c> and <c>pageSize</c> arguments, with the page size capped.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <returns>The page, from 1, and the page size.</returns>
    public static (int Page, int PageSize) ReadPaging(AIFunctionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var page = Math.Max(1, arguments.GetFirstValueOrDefault("page", 1));
        var pageSize = Math.Clamp(arguments.GetFirstValueOrDefault("pageSize", DefaultPageSize), 1, MaxPageSize);

        return (page, pageSize);
    }

    /// <summary>
    /// Converts a stored UTC time to the user's or the site's time zone.
    /// </summary>
    /// <param name="localClock">The local clock.</param>
    /// <param name="utc">The UTC time.</param>
    /// <returns>The local time with its offset, or <see langword="null"/>.</returns>
    public static async Task<string> ToLocalAsync(ILocalClock localClock, DateTime? utc)
    {
        if (utc is null || utc.Value == default || localClock is null)
        {
            return null;
        }

        var local = await localClock.ConvertToLocalAsync(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc));

        return local.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Gets the path of an admin page, for a person to finish what a tool may not do, such as entering a password.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="area">The module that owns the page.</param>
    /// <param name="controller">The controller.</param>
    /// <param name="action">The action.</param>
    /// <param name="values">Other route values, such as the record's ID.</param>
    /// <returns>The path, or <see langword="null"/> when there is no request to build it from.</returns>
    public static string GetAdminPath(IServiceProvider services, string area, string controller, string action, RouteValueDictionary values = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var httpContext = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var linkGenerator = services.GetService<LinkGenerator>();

        if (httpContext is null || linkGenerator is null)
        {
            return null;
        }

        var routeValues = new RouteValueDictionary(values)
        {
            ["area"] = area,
            ["controller"] = controller,
            ["action"] = action,
        };

        return linkGenerator.GetPathByRouteValues(httpContext, null, routeValues);
    }

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <returns>The trimmed text, or <see langword="null"/> when it is missing or blank.</returns>
    public static string ReadString(AIFunctionArguments arguments, string key)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return arguments.TryGetFirstString(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    /// <summary>
    /// Reads an argument that holds a JSON object. A model sends it as an object or as JSON text; both are accepted.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="key">The name of the argument.</param>
    /// <param name="value">A copy of the object, or <see langword="null"/> when the argument is not given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not a JSON object.</returns>
    public static bool TryReadObject(AIFunctionArguments arguments, string key, out JsonObject value)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        value = null;

        if (!arguments.TryGetFirst(key, out var argument) || argument is null)
        {
            return true;
        }

        value = ToNode(argument) as JsonObject;

        return value is not null;
    }

    /// <summary>
    /// Finds an argument whose name says it holds a secret (a password, key, token, passphrase, certificate or
    /// connection string), at any depth and in any letter case. Tools never take a secret: a person enters it on the
    /// admin page.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <returns>The name of the first such argument, or <see langword="null"/> when there is none.</returns>
    public static string FindSecretArgument(AIFunctionArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        foreach (var (name, value) in arguments)
        {
            var node = ToNode(value);

            // A secret is text, or an object that carries one: a flag such as validateAnyCertificate ends like a secret
            // but is a setting.
            if (IngestionSourceSecrets.IsSecret(name) && (value is string || value is JsonElement { ValueKind: JsonValueKind.String } || IsText(node)))
            {
                return name;
            }

            var found = FindSecret(node);

            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Reads an optional whole number from a JSON object.
    /// </summary>
    /// <param name="json">The object.</param>
    /// <param name="name">The member name.</param>
    /// <param name="value">The number, or <see langword="null"/> when the member is missing or null.</param>
    /// <returns><see langword="false"/> when the member is given but is not a whole number.</returns>
    public static bool TryReadInt(JsonObject json, string name, out int? value)
    {
        value = null;

        if (json is null || !json.TryGetPropertyValue(name, out var node) || node is null)
        {
            return true;
        }

        if (node is JsonValue number && number.TryGetValue<int>(out var parsed))
        {
            value = parsed;

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
    /// Reads an optional true or false from a JSON object.
    /// </summary>
    /// <param name="json">The object.</param>
    /// <param name="name">The member name.</param>
    /// <param name="value">The value, or <see langword="null"/> when the member is missing or null.</param>
    /// <returns><see langword="false"/> when the member is given but is not true or false.</returns>
    public static bool TryReadBool(JsonObject json, string name, out bool? value)
    {
        value = null;

        if (json is null || !json.TryGetPropertyValue(name, out var node) || node is null)
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
    /// Reads an optional text from a JSON object.
    /// </summary>
    /// <param name="json">The object.</param>
    /// <param name="name">The member name.</param>
    /// <param name="value">The text as given, or <see langword="null"/> when the member is missing or null.</param>
    /// <returns><see langword="true"/> when the member is given.</returns>
    public static bool TryReadText(JsonObject json, string name, out string value)
    {
        value = null;

        if (json is null || !json.TryGetPropertyValue(name, out var node))
        {
            return false;
        }

        value = node is JsonValue text && text.TryGetValue<string>(out var raw) ? raw : node?.ToJsonString();

        return true;
    }

    private static string FindSecret(JsonNode node)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var (name, value) in json)
                {
                    if (IngestionSourceSecrets.IsSecret(name) && IsText(value))
                    {
                        return name;
                    }

                    var found = FindSecret(value);

                    if (found is not null)
                    {
                        return found;
                    }
                }

                break;

            case JsonArray array:
                foreach (var item in array)
                {
                    var found = FindSecret(item);

                    if (found is not null)
                    {
                        return found;
                    }
                }

                break;
        }

        return null;
    }

    private static bool IsText(JsonNode node)
        => node is JsonValue text && text.GetValueKind() == JsonValueKind.String;

    private static JsonNode ToNode(object value)
    {
        try
        {
            return value switch
            {
                null => null,
                JsonNode node => node.DeepClone(),
                JsonElement { ValueKind: JsonValueKind.Object } element => JsonObject.Create(element),
                JsonElement { ValueKind: JsonValueKind.Array } element => JsonArray.Create(element),
                JsonElement { ValueKind: JsonValueKind.String } element => ParseObjectText(element.GetString()),
                string text => ParseObjectText(text),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Only text that looks like a JSON object or array is parsed, so a plain value such as a name is not.
    private static JsonNode ParseObjectText(string text)
    {
        var trimmed = text?.Trim();

        return string.IsNullOrEmpty(trimmed) || (trimmed[0] != '{' && trimmed[0] != '[')
            ? null
            : JsonNode.Parse(trimmed);
    }
}
