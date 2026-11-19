using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Contents;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The Service level card in the Service tab of an account's page, for users who work on tickets: which policy the
/// account's tickets get, from an agreement or the account itself, with a link to change the account's own.
/// </summary>
internal sealed class AccountServiceLevelDisplayDriver : ContentDisplayDriver
{
    private readonly TicketAccountServiceLevels _levels;
    private readonly ITicketAccessService _access;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountServiceLevelDisplayDriver(
        TicketAccountServiceLevels levels,
        ITicketAccessService access,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor)
    {
        _levels = levels;
        _access = access;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin
            || user is null
            || (await _access.GetViewScopesAsync(user, httpContext.RequestAborted)).Count == 0)
        {
            return null;
        }

        var canChange = await _authorizationService.AuthorizeAsync(user, CommonPermissions.EditContent, contentItem);
        var canOpenPolicies = await _authorizationService.AuthorizeAsync(user, ServiceLevelsPermissions.ManageServiceLevelPolicies);

        return Initialize<AccountServiceLevelViewModel>("AccountServiceLevel", async model =>
        {
            var described = await _levels.DescribeAsync(contentItem, httpContext.RequestAborted);

            model.AccountId = described.AccountId;
            model.AccountPolicyId = described.AccountPolicyId;
            model.AccountPolicyName = described.AccountPolicyName;
            model.Agreements.AddRange(described.Agreements);
            model.CanChange = canChange;
            model.CanOpenPolicies = canOpenPolicies;
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Service, "1"));
    }
}
