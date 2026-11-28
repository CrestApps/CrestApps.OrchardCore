using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// A worker's own AI tool, as on the My Work pages: it acts only for the signed-in person, as the worker linked to their
/// user account, and never for another worker. It needs the permission the My Work pages ask for.
/// </summary>
internal abstract class WorkforceSelfServiceToolBase : AIFunction
{
    /// <summary>
    /// What a tool says when nobody is signed in.
    /// </summary>
    public const string SignInRequired = "Sign in first: this tool works only for your own work.";

    /// <summary>
    /// What a tool says when the person may not use My Work.
    /// </summary>
    public const string NotAllowed = "You can't use My Work, so you can't do this here. Ask a manager to give you access.";

    /// <summary>
    /// What a tool says when the person's account isn't linked to a worker.
    /// </summary>
    public const string NoWorker = "Your account isn't linked to a worker, so you have no shifts to see or claim. Ask a manager to link you.";

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        // The chat hubs publish the caller through IUserAccessor for the whole invocation; the request's user is the fallback.
        var published = arguments.Services?.GetService<IUserAccessor>()?.User;
        var user = published?.Identity?.IsAuthenticated == true
            ? published
            : arguments.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return SignInRequired;
        }

        // The My Work pages ask for this permission before anything else.
        if (!await arguments.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkforcePermissions.UseWorkforcePortal))
        {
            return NotAllowed;
        }

        // The worker is always the one linked to the caller's own account, as on My Work; no argument can name another.
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var worker = string.IsNullOrEmpty(userId)
            ? null
            : await arguments.Services.GetRequiredService<IStaffMemberManager>().FindByUserIdAsync(userId, cancellationToken);

        if (worker is null)
        {
            return NoWorker;
        }

        return await InvokeAsync(arguments, arguments.Services, user, worker, cancellationToken);
    }

    /// <summary>
    /// Runs the tool for the signed-in worker.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The request's services.</param>
    /// <param name="user">The person using the AI.</param>
    /// <param name="worker">The worker linked to the person's account.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The tool's result.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, StaffMember worker, CancellationToken cancellationToken);

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);
}
