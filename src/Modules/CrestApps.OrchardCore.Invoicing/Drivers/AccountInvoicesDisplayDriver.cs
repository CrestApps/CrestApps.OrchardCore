using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Drivers;

/// <summary>
/// The Invoices section of an account's page: what the account owes and is overdue on, its latest invoices, and a new
/// invoice billed to it.
/// </summary>
public sealed class AccountInvoicesDisplayDriver : ContentDisplayDriver
{
    private const int _shown = 20;

    // Totals are worked out from the account's open invoices, a bounded page of them.
    private const int _openLimit = 500;

    private readonly IInvoiceManager _invoices;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILocalClock _localClock;

    public AccountInvoicesDisplayDriver(
        IInvoiceManager invoices,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        ILocalClock localClock)
    {
        _invoices = invoices;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _localClock = localClock;
    }

    public override bool CanHandleModel(ContentItem contentItem)
        => contentItem.Has<AccountPart>();

    public override async Task<IDisplayResult> DisplayAsync(ContentItem contentItem, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (context.DisplayType != OrchardCoreConstants.DisplayType.DetailAdmin ||
            !await _authorizationService.AuthorizeAsync(user, InvoicingPermissions.ViewInvoices))
        {
            return null;
        }

        var billTo = new PartyRef(PartyKinds.Account, contentItem.ContentItemId);
        var canCreate = await _authorizationService.AuthorizeAsync(user, InvoicingPermissions.ManageInvoices);

        return Initialize<AccountInvoicesViewModel>("AccountInvoices", async model =>
        {
            var latest = await _invoices.PageAsync(1, _shown, new InvoiceQuery { BillTo = [billTo], OnlyBillTo = true });
            var open = await _invoices.PageAsync(1, _openLimit, new InvoiceQuery { BillTo = [billTo], OnlyBillTo = true, Status = InvoiceStatus.Open });
            var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);

            model.BillTo = billTo.ToString();
            model.Invoices = [.. latest.Entries];
            model.Count = latest.Count;
            model.CanCreate = canCreate;
            model.Totals = [.. open.Entries
                .Where(invoice => invoice.AmountDue > 0m)
                .GroupBy(invoice => invoice.Currency, StringComparer.OrdinalIgnoreCase)
                .Select(group => (group.Key, group.Sum(invoice => invoice.AmountDue), group.Where(invoice => invoice.DueDate < today).Sum(invoice => invoice.AmountDue)))];
        }).Location(OrchardCoreConstants.DisplayType.DetailAdmin, AccountPageTabs.Location(AccountPageTabs.Invoices));
    }
}
