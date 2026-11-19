using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Approvals.Deployments;

/// <summary>
/// Shows the approval requests step, and refuses to add it to a plan for a user who may not view requests.
/// </summary>
internal sealed class ApprovalRequestsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, ApprovalRequestsDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public ApprovalRequestsDeploymentStepDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<ApprovalRequestsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ApprovalRequestsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ApprovalRequestsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("ApprovalRequestsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(ApprovalRequestsDeploymentStep step, BuildEditorContext context)
    {
        return View("ApprovalRequestsDeploymentStep_Fields_Edit", step).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ApprovalRequestsDeploymentStep step, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ApprovalsPermissions.ViewApprovalRequests))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Only a user who may view approval requests can export them."]);
        }

        return Edit(step, context);
    }
}
