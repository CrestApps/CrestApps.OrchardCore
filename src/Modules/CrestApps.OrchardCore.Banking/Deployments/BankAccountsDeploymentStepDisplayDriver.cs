using CrestApps.OrchardCore.Banking.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Deployments;

internal sealed class BankAccountsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, BankAccountsDeploymentStep>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public BankAccountsDeploymentStepDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IStringLocalizer<BankAccountsDeploymentStepDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(BankAccountsDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("BankAccountsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("BankAccountsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(BankAccountsDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<BankAccountsDeploymentStepViewModel>("BankAccountsDeploymentStep_Fields_Edit", model => model.IncludeSensitiveData = step.IncludeSensitiveData).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(BankAccountsDeploymentStep step, UpdateEditorContext context)
    {
        var model = new BankAccountsDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.IncludeSensitiveData && !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, BankingPermissions.RevealBankAccountNumbers))
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.IncludeSensitiveData), S["Only a user who may reveal bank account numbers can export them."]);
        }
        else
        {
            step.IncludeSensitiveData = model.IncludeSensitiveData;
        }

        return Edit(step, context);
    }
}
