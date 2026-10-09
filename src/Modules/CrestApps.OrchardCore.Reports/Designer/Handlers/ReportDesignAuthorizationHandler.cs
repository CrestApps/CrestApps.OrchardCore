using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Designer.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Reports.Designer.Handlers;

/// <summary>
/// Grants designed reports and views to the people they belong to, the way Orchard Core grants a content item to its
/// owner. A report is always authorized against a broad permission with the report as the resource:
/// <list type="bullet">
///   <item><see cref="ReportDesignerPermissions.ViewAllReportDesigns"/> to run it, granted here to its owner, to the
///   users it is shared with, to members of the roles it is shared with, and to everyone when it is shared with the
///   Anonymous role (to every signed-in person for the Authenticated role);</item>
///   <item><see cref="ReportDesignerPermissions.ManageAllReportDesigns"/> to change it, granted here to its owner when
///   they hold <see cref="ReportDesignerPermissions.ManageOwnReportDesigns"/>.</item>
/// </list>
/// A view is a shared building block for designers: any user who holds
/// <see cref="ReportDesignerPermissions.ManageOwnReportDesigns"/> may read it, and its owner may change it.
/// </summary>
internal sealed class ReportDesignAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<IAuthorizationService> _authorizationService;

    /// <param name="authorizationService">
    /// The authorization service, resolved lazily because it is what runs this handler.
    /// </param>
    public ReportDesignAuthorizationHandler(Lazy<IAuthorizationService> authorizationService)
    {
        _authorizationService = authorizationService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded || context.Resource is null)
        {
            return;
        }

        var name = requirement.Permission.Name;
        var isView = name == ReportDesignerPermissions.ViewAllReportDesigns.Name;
        var isManage = name == ReportDesignerPermissions.ManageAllReportDesigns.Name;

        if (!isView && !isManage)
        {
            return;
        }

        switch (context.Resource)
        {
            case ReportDesign design:
                if ((isView && IsSharedWith(design, context.User)) ||
                    (IsOwner(design.OwnerId, context.User) && (isView || await CanDesignAsync(context.User))))
                {
                    context.Succeed(requirement);
                }

                break;

            case ReportView view:
                if ((isView || IsOwner(view.OwnerId, context.User)) && await CanDesignAsync(context.User))
                {
                    context.Succeed(requirement);
                }

                break;
        }
    }

    /// <summary>
    /// Determines whether a report is shared with a user through its user or role lists.
    /// </summary>
    /// <param name="design">The report.</param>
    /// <param name="user">The user, who may be anonymous.</param>
    /// <returns><see langword="true"/> when the report is shared with the user.</returns>
    internal static bool IsSharedWith(ReportDesign design, ClaimsPrincipal user)
    {
        var roles = design.SharedRoles ?? [];

        if (roles.Contains(OrchardCoreConstants.Roles.Anonymous, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (user?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (roles.Contains(OrchardCoreConstants.Roles.Authenticated, StringComparer.OrdinalIgnoreCase) ||
            roles.Any(user.IsInRole))
        {
            return true;
        }

        var userName = user.Identity.Name;

        return !string.IsNullOrEmpty(userName) &&
            (design.SharedUserNames ?? []).Contains(userName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsOwner(string ownerId, ClaimsPrincipal user)
    {
        var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return !string.IsNullOrEmpty(ownerId) &&
            !string.IsNullOrEmpty(userId) &&
            string.Equals(ownerId, userId, StringComparison.Ordinal);
    }

    private async Task<bool> CanDesignAsync(ClaimsPrincipal user)
    {
        return user?.Identity?.IsAuthenticated == true &&
            await _authorizationService.Value.AuthorizeAsync(user, ReportDesignerPermissions.ManageOwnReportDesigns);
    }
}
