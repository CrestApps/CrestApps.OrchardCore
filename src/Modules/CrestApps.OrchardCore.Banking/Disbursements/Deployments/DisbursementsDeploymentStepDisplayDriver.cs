using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Disbursements.Deployments;

/// <summary>
/// Shows the disbursements step, and refuses to add it to a plan for a user who may not view disbursements.
/// </summary>
internal sealed class DisbursementsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, DisbursementsDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public DisbursementsDeploymentStepDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<DisbursementsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(DisbursementsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("DisbursementsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("DisbursementsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(DisbursementsDeploymentStep step, BuildEditorContext context)
    {
        return View("DisbursementsDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Disbursements"], "10"));
    }

    public override async Task<IDisplayResult> UpdateAsync(DisbursementsDeploymentStep step, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, DisbursementsPermissions.ViewDisbursements))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Only a user who may view disbursements can export them."]);
        }

        return Edit(step, context);
    }
}
