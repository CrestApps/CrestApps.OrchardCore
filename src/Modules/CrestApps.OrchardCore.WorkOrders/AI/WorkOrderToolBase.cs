using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// A work order or service agreement AI tool. It works only for a signed-in person who holds the permission the matching
/// admin page asks for, and it acts as that person.
/// </summary>
internal abstract class WorkOrderToolBase : AIFunction
{
    /// <summary>
    /// What a tool says when nobody is signed in.
    /// </summary>
    public const string SignInRequired = "Only a signed-in person who may work with work orders can use this tool.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Gets the permission the matching admin page asks for.
    /// </summary>
    protected abstract Permission Permission { get; }

    /// <summary>
    /// Gets what a tool says when the person lacks <paramref name="permission"/>.
    /// </summary>
    /// <param name="permission">The permission.</param>
    /// <returns>The sentence.</returns>
    public static string NotAllowed(Permission permission)
        => $"The user isn't allowed to do this: it needs the '{permission?.Name}' permission.";

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        // The chat hubs publish the caller through IUserAccessor for the whole invocation; the request's user is the fallback.
        var published = arguments.Services?.GetService<IUserAccessor>()?.User;
        var user = published?.Identity?.IsAuthenticated == true
            ? published
            : arguments.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        // Refused before the permission check, so a permission granted to the Anonymous role never opens these tools.
        if (user?.Identity?.IsAuthenticated != true)
        {
            return SignInRequired;
        }

        if (!await arguments.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, Permission))
        {
            return NotAllowed(Permission);
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for a signed-in person who holds the permission.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="user">The person using the AI.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);
}
