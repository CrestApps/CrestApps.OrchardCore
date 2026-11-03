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
/// Shows a CRM contact's or account's latest work orders on its page, with a button to start one for it.
/// </summary>
internal sealed class ContactWorkOrdersDisplayDriver : ContentDisplayDriver
{
    private const int MaxRows = 10;

    private readonly ISession _session;
    private readonly WorkOrderSettingManager<WorkOrderType> _types;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ContactWorkOrdersDisplayDriver(ISession session, WorkOrderSettingManager<WorkOrderType> types, IAuthorizationService authorizationService, IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _types = types;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<OmnichannelContactPart>() || contentItem.Has<AccountPart>();

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
            var isAccount = contentItem.Has<AccountPart>();
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
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, "Content:20");
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
