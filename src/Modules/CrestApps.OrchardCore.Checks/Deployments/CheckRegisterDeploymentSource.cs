using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checks.Core.Services;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Recipes;
using CrestApps.OrchardCore.Checks.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Checks.Deployments;

/// <summary>
/// Writes the check register to a deployment plan, a page at a time. A plan run from a request runs as its user: it
/// needs <see cref="ChecksPermissions.ViewCheckRegister"/> and writes only the checks of the sources the user may see.
/// </summary>
internal sealed class CheckRegisterDeploymentSource : DeploymentSourceBase<CheckRegisterDeploymentStep>
{
    private const int PageSize = 500;

    private readonly ICheckRecordStore _store;
    private readonly ICheckAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public CheckRegisterDeploymentSource(
        ICheckRecordStore store,
        ICheckAccessService access,
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
        var access = CheckAccessFilter.Unrestricted;

        // A plan run without a request, such as a remote deployment, writes what the step was saved for.
        if (user?.Identity?.IsAuthenticated == true)
        {
            if (!await _authorizationService.AuthorizeAsync(user, ChecksPermissions.ViewCheckRegister))
            {
                await _notifier.WarningAsync(H["The check register was left out of the plan: you may not view it."]);

                return;
            }

            access = await _access.GetFilterAsync(user, CheckAccessLevel.View);
        }

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
