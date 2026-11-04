using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.WorkOrders.Agreements;

/// <summary>
/// Shows a CRM account's service agreements on its page, with a button to make one for it.
/// </summary>
internal sealed class AccountAgreementsDisplayDriver : ContentDisplayDriver
{
    private const int MaxRows = 10;

    private readonly ISession _session;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccountAgreementsDisplayDriver(ISession session, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin || !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, WorkOrdersPermissions.ManageServiceAgreements))
        {
            return null;
        }

        return Initialize<AccountAgreementsViewModel>("AccountAgreements", async model =>
        {
            var accountId = contentItem.ContentItemId;
            var query = _session.Query<ServiceAgreement, ServiceAgreementIndex>(index => index.AccountId == accountId, collection: WorkOrdersConstants.CollectionName);

            model.AccountId = accountId;
            model.Count = await query.CountAsync();
            model.Agreements = [.. await query.OrderByDescending(index => index.Number).Take(MaxRows).ListAsync()];
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Service, "3"));
    }
}

/// <summary>
/// An account's service agreements.
/// </summary>
public class AccountAgreementsViewModel
{
    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets how many agreements the account has.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the latest agreements.
    /// </summary>
    public IList<ServiceAgreement> Agreements { get; set; } = [];
}
