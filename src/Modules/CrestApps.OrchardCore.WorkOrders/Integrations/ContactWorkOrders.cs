using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
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

namespace CrestApps.OrchardCore.WorkOrders.Integrations;

/// <summary>
/// Shows a CRM contact's latest work orders on its page, with a button to start one for it.
/// </summary>
internal sealed class ContactWorkOrdersDisplayDriver : OwnerWorkOrdersDisplayDriver
{
    public ContactWorkOrdersDisplayDriver(ISession session, WorkOrderSettingManager<WorkOrderType> types, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        : base(session, types, authorizationService, httpContextAccessor, false)
    {
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<OmnichannelContactPart>();
}

/// <summary>
/// Shows an account's latest work orders on its page, with a button to start one for it.
/// </summary>
internal sealed class AccountWorkOrdersDisplayDriver : OwnerWorkOrdersDisplayDriver
{
    public AccountWorkOrdersDisplayDriver(ISession session, WorkOrderSettingManager<WorkOrderType> types, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
        : base(session, types, authorizationService, httpContextAccessor, true)
    {
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();
}

/// <summary>
/// Shows a contact's or an account's latest work orders on its page.
/// </summary>
internal abstract class OwnerWorkOrdersDisplayDriver : ContentDisplayDriver
{
    private const int MaxRows = 10;

    private readonly ISession _session;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _isAccount;

    protected OwnerWorkOrdersDisplayDriver(ISession session, WorkOrderSettingManager<WorkOrderType> types, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor, bool isAccount)
    {
        _session = session;
        _types = types;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _isAccount = isAccount;
    }

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin || !await _authorizationService.AuthorizeAsync(user, WorkOrdersPermissions.ViewWorkOrders))
        {
            return null;
        }

        var canManage = await _authorizationService.AuthorizeAsync(user, WorkOrdersPermissions.ManageWorkOrders);

        return Initialize<ContactWorkOrdersViewModel>("ContactWorkOrders", async model =>
        {
            var id = contentItem.ContentItemId;
            var isAccount = _isAccount;
            var query = isAccount
                ? _session.Query<WorkOrder, WorkOrderIndex>(index => index.AccountId == id, collection: WorkOrdersConstants.CollectionName)
                : _session.Query<WorkOrder, WorkOrderIndex>(index => index.ContactId == id, collection: WorkOrdersConstants.CollectionName);

            model.IsAccount = isAccount;
            model.ContactId = isAccount ? null : id;
            model.AccountId = isAccount ? id : null;
            model.ContactName = isAccount ? null : contentItem.DisplayText;
            model.Count = await query.CountAsync();
            model.WorkOrders = [.. await query.OrderByDescending(index => index.CreatedUtc).Take(MaxRows).ListAsync()];
            model.Types = canManage ? [.. (await _types.GetAllAsync()).OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase)] : [];
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, _isAccount ? AccountPageTabs.Location(AccountPageTabs.Service, "1") : "Content:21");
    }
}

/// <summary>
/// A contact's work orders.
/// </summary>
public class ContactWorkOrdersViewModel
{
    /// <summary>
    /// Gets or sets the contact.
    /// </summary>
    public string ContactId { get; set; }

    /// <summary>
    /// Gets or sets the contact's name.
    /// </summary>
    public string ContactName { get; set; }

    /// <summary>
    /// Gets or sets the account, when the page is an account's.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets whether the page is an account's.
    /// </summary>
    public bool IsAccount { get; set; }

    /// <summary>
    /// Gets or sets how many work orders the contact has.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the latest work orders.
    /// </summary>
    public IList<WorkOrder> WorkOrders { get; set; } = [];

    /// <summary>
    /// Gets or sets the types a new one can be, when the user can make work orders.
    /// </summary>
    public IList<WorkOrderType> Types { get; set; } = [];
}
