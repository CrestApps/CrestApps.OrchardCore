using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Banking.Disbursements.Core.Services;
using CrestApps.OrchardCore.Banking.Disbursements.Recipes;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Banking.Disbursements.Deployments;

/// <summary>
/// Writes disbursements to a deployment plan, a page at a time. A plan run from a request runs as its user: it needs
/// <see cref="DisbursementsPermissions.ViewDisbursements"/> and writes only the disbursements of the sources the user
/// may see. A run without a signed-in person writes only those no source restricts.
/// </summary>
internal sealed class DisbursementsDeploymentSource : DeploymentSourceBase<DisbursementsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly IDisbursementStore _store;
    private readonly ISourceAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public DisbursementsDeploymentSource(
        IDisbursementStore store,
        ISourceAccessService access,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<DisbursementsDeploymentSource> htmlLocalizer)
    {
        _store = store;
        _access = access;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    protected override async Task ProcessAsync(DisbursementsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var signedIn = user?.Identity?.IsAuthenticated == true;

        if (signedIn && !await _authorizationService.AuthorizeAsync(user, DisbursementsPermissions.ViewDisbursements))
        {
            await _notifier.WarningAsync(H["The disbursements were left out of the plan: you may not view them."]);

            return;
        }

        var query = new DisbursementQuery
        {
            OldestFirst = true,
            Access = await _access.GetFilterAsync(signedIn ? user : null, SourceAccessLevel.View),
        };
        var disbursements = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var (_, page) = await _store.ListPageAsync(query, skip, PageSize);

            foreach (var disbursement in page)
            {
                var node = JsonSerializer.SerializeToNode(disbursement, DisbursementsRecipeStep.JsonOptions).AsObject();

                node.Remove("id");
                node.Remove("properties");
                disbursements.Add(node);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [DisbursementsRecipeStep.DisbursementsProperty] = disbursements,
        });
    }
}
