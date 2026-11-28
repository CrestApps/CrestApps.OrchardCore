using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.AI;

/// <summary>
/// A user profile AI tool. It acts as the person using the AI: it refuses a caller who is not signed in and any argument
/// its schema does not declare, and each tool checks the permission the user editor checks, with the user as resource.
/// </summary>
internal abstract class UsersAIToolBase : AIFunction
{
    /// <summary>
    /// The answer to a caller who is not signed in or lacks the permission.
    /// </summary>
    public const string NotAllowedMessage = "You are not allowed to use this function.";

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

        if (caller is null)
        {
            return NotAllowedMessage;
        }

        _argumentNames ??= GetArgumentNames(JsonSchema);

        if (arguments.Keys.FirstOrDefault(name => !_argumentNames.Contains(name)) is { } unknown)
        {
            return $"Nothing was changed: '{unknown}' is not an argument of {Name}. Use only the arguments the tool declares.";
        }

        return await InvokeAsync(arguments, services, caller, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in caller.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken);

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
    /// Checks a permission for the caller, with a resource.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="permission">The permission.</param>
    /// <param name="resource">The resource, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the caller has the permission.</returns>
    protected static async Task<bool> IsAuthorizedAsync(IServiceProvider services, ClaimsPrincipal caller, Permission permission, object resource = null)
    {
        var authorizationService = services.GetService<IAuthorizationService>();

        return authorizationService is not null && await authorizationService.AuthorizeAsync(caller, permission, resource);
    }

    /// <summary>
    /// Finds a user by id, or by user name or email when no user has that id.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="userIdOrName">The user id, user name or email.</param>
    /// <returns>The user, or <see langword="null"/>.</returns>
    protected static async Task<User> FindUserAsync(IServiceProvider services, string userIdOrName)
    {
        if (string.IsNullOrEmpty(userIdOrName))
        {
            return null;
        }

        var userManager = services.GetRequiredService<UserManager<IUser>>();

        return (await userManager.FindByIdAsync(userIdOrName)
            ?? await userManager.FindByNameAsync(userIdOrName)
            ?? await userManager.FindByEmailAsync(userIdOrName)) as User;
    }

    /// <summary>
    /// Saves a user through the user manager, as the user editor does, so the user event handlers and indexes run.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="user">The user.</param>
    /// <returns>The errors, or an empty list.</returns>
    protected static async Task<IReadOnlyList<string>> UpdateUserAsync(IServiceProvider services, User user)
    {
        var result = await services.GetRequiredService<UserManager<IUser>>().UpdateAsync(user);

        return result.Succeeded
            ? []
            : result.Errors.Select(error => error.Description).ToList();
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
    /// Returns whether an argument was given, even blank.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns><see langword="true"/> when the argument is present and not null.</returns>
    protected static bool HasArgument(AIFunctionArguments arguments, string name)
        => arguments is not null &&
            arguments.TryGetValue(name, out var value) &&
            value is not null &&
            value is not JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined };

    /// <summary>
    /// Reads a yes or no argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/> when it is missing.</returns>
    protected static bool? ReadBool(AIFunctionArguments arguments, string name)
        => bool.TryParse(ReadString(arguments, name), out var value) ? value : null;

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
