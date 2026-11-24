using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// A Time Tracker AI tool. It works only for a signed-in person who holds the permission the matching page asks for, and
/// it acts as that person.
/// </summary>
internal abstract class TimeTrackerToolBase : AIFunction
{
    /// <summary>
    /// What a manager's tool says when nobody is signed in.
    /// </summary>
    public const string SignInRequired = "Only a signed-in person who may work with tracked time can use this tool.";

    /// <summary>
    /// What a person's own clock tool says when nobody is signed in.
    /// </summary>
    public const string SelfSignInRequired = "Sign in to see or track your own time.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <summary>
    /// Gets the permission the matching page asks for.
    /// </summary>
    protected abstract Permission Permission { get; }

    /// <summary>
    /// Gets what the tool says when nobody is signed in.
    /// </summary>
    protected virtual string SignInMessage => SignInRequired;

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
            return SignInMessage;
        }

        if (!await IsAllowedAsync(arguments.Services, user, cancellationToken))
        {
            return NotAllowed(Permission);
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Checks the person's permission. By default the person must hold <see cref="Permission"/>.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="user">The signed-in person.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>Whether the person may use the tool.</returns>
    protected virtual async Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
        => await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, Permission);

    /// <summary>
    /// Whether the person's time is kept by another clock that takes punches from Time Tracker (such as the Workforce time
    /// clock). That clock applies its own permissions (on Workforce: Use the My Work portal and Clock in and out), so a
    /// worker who punches there needs no Time Tracker permission, as on the My Work page.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="user">The signed-in person.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns><see langword="true"/> when another clock that takes punches keeps the person's time.</returns>
    protected static async Task<bool> IsKeptByPunchClockAsync(IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        var external = await TimeTrackerTools.ExternalClockAsync(services, userId, cancellationToken);

        return external is not null && services.GetServices<IExternalTimeClockPunches>().Any(clock => clock.ClockId == external.Id);
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
