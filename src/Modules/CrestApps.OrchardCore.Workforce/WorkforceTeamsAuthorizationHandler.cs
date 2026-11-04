using CrestApps.OrchardCore.Teams;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// Lets a user with Manage workforce teams manage the shared teams, which Workforce's teams are, so roles given that
/// permission before teams moved to their own module keep their access.
/// </summary>
internal sealed class WorkforceTeamsAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<IAuthorizationService> _authorizationService;

    public WorkforceTeamsAuthorizationHandler(Lazy<IAuthorizationService> authorizationService)
    {
        _authorizationService = authorizationService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded || context.User is null)
        {
            return;
        }

        if (requirement.Permission.Name == TeamsPermissions.ManageTeams.Name
            && await _authorizationService.Value.AuthorizeAsync(context.User, WorkforcePermissions.ManageTeams))
        {
            context.Succeed(requirement);
        }
    }
}
