using CrestApps.Core.Security;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Learning.AI.Tools;

/// <summary>
/// A Learning AI tool. It acts as the person using the AI: it refuses anyone who is not signed in or lacks its
/// permission, and reads only what that person may read.
/// </summary>
public abstract class LearningAIToolBase : AIFunction
{
    /// <summary>
    /// The options the tools write their results with.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Gets the permission the person must hold to use the tool, or <see langword="null"/> when the tool decides from the
    /// records it reads (the staff lists check the person's scope themselves).
    /// </summary>
    protected abstract Permission Permission { get; }

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var services = arguments.Services;
        var user = services?.GetService<IUserAccessor>()?.User ?? services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true || string.IsNullOrEmpty(user.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return "This tool works only for a signed-in person.";
        }

        if (Permission is not null && !await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, Permission))
        {
            return "The user isn't allowed to use this tool.";
        }

        return await InvokeAsync(arguments, services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in person who holds its permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="user">The person using the AI.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a string argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : Convert.ToString(value, CultureInfo.InvariantCulture);

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a whole number argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    protected static int? ReadInt(AIFunctionArguments arguments, string name)
        => int.TryParse(Read(arguments, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);

    /// <summary>
    /// Writes a result as JSON.
    /// </summary>
    /// <param name="value">The result.</param>
    /// <returns>The JSON.</returns>
    protected static string Json(object value)
        => JsonSerializer.Serialize(value, JsonOptions);
}
