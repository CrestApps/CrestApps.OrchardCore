using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Checks.Deployments;

/// <summary>
/// Shows the check register step, and refuses to add it to a plan for a user who may not view the register.
/// </summary>
internal sealed class CheckRegisterDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, CheckRegisterDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public CheckRegisterDeploymentStepDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<CheckRegisterDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(CheckRegisterDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("CheckRegisterDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("CheckRegisterDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(CheckRegisterDeploymentStep step, BuildEditorContext context)
    {
        return View("CheckRegisterDeploymentStep_Fields_Edit", step).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(CheckRegisterDeploymentStep step, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ChecksPermissions.ViewCheckRegister))
        {
            context.Updater.ModelState.AddModelError(Prefix, S["Only a user who may view the check register can export it."]);
        }

        return Edit(step, context);
    }
}
