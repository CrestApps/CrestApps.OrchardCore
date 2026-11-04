using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.Skills;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.ContactCenter.SharedSkills;

/// <summary>
/// Lets a user with Manage Contact Center skills manage and assign the shared skills, which Contact Center's skills
/// are, so roles given that permission before keep their access.
/// </summary>
internal sealed class ContactCenterSkillsAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<IAuthorizationService> _authorizationService;

    public ContactCenterSkillsAuthorizationHandler(Lazy<IAuthorizationService> authorizationService)
    {
        _authorizationService = authorizationService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded || context.User is null)
        {
            return;
        }

        var name = requirement.Permission.Name;

        if ((name == SkillsPermissions.ManageSkills.Name || name == SkillsPermissions.AssignSkills.Name)
            && await _authorizationService.Value.AuthorizeAsync(context.User, ContactCenterPermissions.ManageSkills))
        {
            context.Succeed(requirement);
        }
    }
}
