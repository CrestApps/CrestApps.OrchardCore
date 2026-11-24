using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Recipes;
using CrestApps.OrchardCore.SourceAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Approvals.Deployments;

/// <summary>
/// Writes approval requests to a deployment plan, a page at a time. A plan run from a request runs as its user: it
/// needs <see cref="ApprovalsPermissions.ViewApprovalRequests"/> and writes only the requests about subjects the user
/// may see. A run without a signed-in person writes only those no subject kind restricts.
/// </summary>
internal sealed class ApprovalRequestsDeploymentSource : DeploymentSourceBase<ApprovalRequestsDeploymentStep>
{
    private const int PageSize = 500;

    private readonly IApprovalRequestStore _store;
    private readonly ISourceAccessService _access;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public ApprovalRequestsDeploymentSource(
        IApprovalRequestStore store,
        ISourceAccessService access,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ApprovalRequestsDeploymentSource> htmlLocalizer)
    {
        _store = store;
        _access = access;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    protected override async Task ProcessAsync(ApprovalRequestsDeploymentStep step, DeploymentPlanResult result)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var signedIn = user?.Identity?.IsAuthenticated == true;

        if (signedIn && !await _authorizationService.AuthorizeAsync(user, ApprovalsPermissions.ViewApprovalRequests))
        {
            await _notifier.WarningAsync(H["The approval requests were left out of the plan: you may not view them."]);

            return;
        }

        var query = new ApprovalRequestQuery
        {
            OldestFirst = true,
            Access = await _access.GetFilterAsync(signedIn ? user : null, SourceAccessLevel.View),
        };
        var requests = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var (_, page) = await _store.ListPageAsync(query, skip, PageSize);

            foreach (var request in page)
            {
                var node = JsonSerializer.SerializeToNode(request, ApprovalRequestsRecipeStep.JsonOptions).AsObject();

                node.Remove("id");
                node.Remove("properties");
                requests.Add(node);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [ApprovalRequestsRecipeStep.RequestsProperty] = requests,
        });
    }
}
