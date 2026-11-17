using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Recipes;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Banking.Checks.Deployments;

/// <summary>
/// Writes the check register to a deployment plan, a page at a time. A plan run from a request runs as its user: it
/// needs <see cref="ChecksPermissions.ViewCheckRegister"/> and writes only the checks of the sources the user may see.
/// A run without a signed-in person writes only the checks no source restricts.
/// </summary>
internal sealed class CheckRegisterDeploymentSource : DeploymentSourceBase<CheckRegisterDeploymentStep>
{
    private const int PageSize = 500;

    private readonly ICheckRecordStore _store;
    private readonly ISourceAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public CheckRegisterDeploymentSource(
        ICheckRecordStore store,
        ISourceAccessService access,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<CheckRegisterDeploymentSource> htmlLocalizer)
    {
        _store = store;
        _access = access;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    protected override async Task ProcessAsync(CheckRegisterDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated == true && !await _authorizationService.AuthorizeAsync(user, ChecksPermissions.ViewCheckRegister))
        {
            await _notifier.WarningAsync(H["The check register was left out of the plan: you may not view it."]);

            return;
        }

        // A plan run without a signed-in person, such as a remote deployment, fails closed: it writes only the checks
        // no source restricts, since saving the step needed only View check register.
        var access = await _access.GetFilterAsync(user?.Identity?.IsAuthenticated == true ? user : null, SourceAccessLevel.View);

        var checks = new JsonArray();
        var query = new CheckRegisterQuery { OldestFirst = true, Access = access };

        for (var skip = 0; ; skip += PageSize)
        {
            var (_, page) = await _store.ListPageAsync(query, skip, PageSize);

            foreach (var check in page)
            {
                var node = JsonSerializer.SerializeToNode(check, CheckRegisterRecipeStep.JsonOptions).AsObject();

                node.Remove("id");
                node.Remove("properties");
                checks.Add(node);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [CheckRegisterRecipeStep.ChecksProperty] = checks,
        });
    }
}
