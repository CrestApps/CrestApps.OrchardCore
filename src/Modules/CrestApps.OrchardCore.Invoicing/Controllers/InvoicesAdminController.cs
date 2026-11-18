using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Endpoints;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Navigation;
using OrchardCore.Routing;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// The invoices of the admin: the list, the draft editor, the invoice's page and its actions (issue, void, write off,
/// reinstate and delete a draft).
/// </summary>
[Admin("invoices/{action}/{itemId?}", "Invoices{action}")]
public sealed class InvoicesAdminController : Controller
{
    private const string _optionsSearch = "Options.Search";
    private const string _optionsStatus = "Options.Status";
    private const string _optionsSettlement = "Options.Settlement";
    private const string _submitIssue = "issue";
    private const string _submitUpdate = "update";

    private readonly IInvoiceManager _invoiceManager;
    private readonly IInvoiceCommandService _commands;
    private readonly InvoiceLedgerSync _ledgerSync;
    private readonly IDocumentCalculator _calculator;
    private readonly IPaymentTermManager _paymentTerms;
    private readonly IPartyDirectory _parties;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<Invoice> _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public InvoicesAdminController(
        IInvoiceManager invoiceManager,
        IInvoiceCommandService commands,
        InvoiceLedgerSync ledgerSync,
        IDocumentCalculator calculator,
        IPaymentTermManager paymentTerms,
        IPartyDirectory parties,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IDisplayManager<Invoice> displayManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
        INotifier notifier,
        IHtmlLocalizer<InvoicesAdminController> htmlLocalizer,
        IStringLocalizer<InvoicesAdminController> stringLocalizer)
    {
        _invoiceManager = invoiceManager;
        _commands = commands;
        _ledgerSync = ledgerSync;
        _calculator = calculator;
        _paymentTerms = paymentTerms;
        _parties = parties;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the invoices, newest first, with a search and the status and payment filters.
    /// </summary>
    [Admin("invoices", "InvoicesIndex")]
    public async Task<IActionResult> Index(
        InvoicesAdminIndexOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value);
        var result = await _invoiceManager.PageAsync(pager.Page, pager.PageSize, new InvoiceQuery
        {
            Search = options.Search,
            Status = options.Status,
            Settlement = options.Settlement,
        });

        var routeData = new RouteData();
        AddFilterRouteValues(options, routeData.Values);

        options.Statuses = BuildStatusFilterItems(options.Status);
        options.Settlements = BuildSettlementFilterItems(options.Settlement);

        // The filters render at the end of the list's toolbar.
        var filters = await shapeFactory.CreateAdminListFiltersAsync(
            "Invoices",
            "invoices-filter",
            new AdminListFilter(_optionsStatus, options.Statuses, S["Filter by status"], S["Status"]),
            new AdminListFilter(_optionsSettlement, options.Settlements, S["Filter by payment"], S["Payment"]));

        return View(new InvoicesAdminIndexViewModel
        {
            Options = options,
            CanCreate = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices),
            List = new AdminListContext<Invoice>
            {
                Source = result.Entries,
                ToolbarActions = filters,
                Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
            },
        });
    }

    /// <summary>
    /// Keeps the search and filters in the address when the list's form is submitted.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("invoices", "InvoicesIndex")]
    public async Task<IActionResult> IndexFilterPost(InvoicesAdminIndexOptions options, PagerParameters pagerParameters)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var routeValues = new RouteValueDictionary();
        AddFilterRouteValues(options, routeValues);

        if (pagerParameters.PageSize.HasValue)
        {
            routeValues.TryAdd("pageSize", pagerParameters.PageSize.Value);
        }

        return RedirectToAction(nameof(Index), routeValues);
    }

    /// <summary>
    /// Opens a new draft, with the defaults of the invoicing settings.
    /// </summary>
    public async Task<IActionResult> Create(string billTo)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var settings = await GetSettingsAsync();
        var invoice = await _invoiceManager.NewAsync();

        invoice.Currency = settings.DefaultCurrency;
        invoice.PaymentTermId = settings.DefaultPaymentTermId;
        invoice.PricesIncludeTax = settings.PricesIncludeTax;
        invoice.AllowPartialPayment = settings.AllowPartialPayment;

        if (PartyRef.TryParse(billTo, out var party))
        {
            invoice.BillTo.Party = party;
        }

        var model = new InvoiceEditViewModel();
        InvoiceDraftEditor.Build(invoice, model);
        model.ItemId = null;

        return View("Edit", await PrepareAsync(model, invoice));
    }

    /// <summary>
    /// Saves a new draft, and issues it when asked.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(InvoiceEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var invoice = await _invoiceManager.NewAsync();

        return await SaveAsync(invoice, model, submit, isNew: true);
    }

    /// <summary>
    /// Opens a draft in the editor. An issued invoice can't be changed, so it opens its page instead.
    /// </summary>
    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        if (!InvoiceLifecycle.CanEdit(invoice))
        {
            await _notifier.WarningAsync(H["An issued invoice can't be changed. Correct it with a credit note."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        var model = new InvoiceEditViewModel();
        InvoiceDraftEditor.Build(invoice, model);

        return View(await PrepareAsync(model, invoice));
    }

    /// <summary>
    /// Saves a draft, and issues it when asked.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, InvoiceEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        if (!InvoiceLifecycle.CanEdit(invoice))
        {
            await _notifier.WarningAsync(H["An issued invoice can't be changed. Correct it with a credit note."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        return await SaveAsync(invoice, model, submit, isNew: false);
    }

    /// <summary>
    /// Shows an invoice, built by the display drivers of <see cref="Invoice"/> so another module can add a card to it.
    /// </summary>
    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        // Catches up anything the ledger recorded while the invoice was busy, so the page shows what is paid and due now.
        invoice = await _ledgerSync.SyncAsync(invoice, HttpContext.RequestAborted);

        return View(new InvoiceDetailPageViewModel
        {
            Invoice = invoice,
            Shape = await _displayManager.BuildDisplayAsync(invoice, _updateModelAccessor.ModelUpdater, InvoicingConstants.DisplayTypes.DetailAdmin),
        });
    }

    /// <summary>
    /// Shows the invoice as the customer gets it, to print.
    /// </summary>
    public async Task<IActionResult> Document(string itemId, [FromServices] IInvoiceDocumentBuilder builder)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(new InvoiceDocumentPageViewModel
        {
            Source = invoice,
            Document = await builder.BuildAsync(invoice, HttpContext.RequestAborted),
            PdfUrl = Url.Action(nameof(Pdf), new { itemId }),
            BackUrl = Url.Action(nameof(Detail), new { itemId }),
        });
    }

    /// <summary>
    /// Downloads the invoice's PDF: the one stored when it was issued, or for a draft one marked as such.
    /// </summary>
    public async Task<IActionResult> Pdf(string itemId, [FromServices] IInvoicePdfService pdfService)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        var pdf = await pdfService.GetAsync(invoice, HttpContext.RequestAborted);

        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>
    /// Opens the send dialog: who the invoice goes to, with its PDF attached.
    /// </summary>
    public async Task<IActionResult> Send(string itemId, [FromServices] ICommerceEmailSender emailSender)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(await PrepareSendAsync(new InvoiceSendViewModel
        {
            ItemId = invoice.ItemId,
            To = invoice.BillTo?.Contact?.Email,
            Cc = invoice.BillTo?.CopyTo is { Count: > 0 } copies ? string.Join(", ", copies) : null,
        }, invoice, emailSender));
    }

    /// <summary>
    /// Sends the invoice, issuing a draft first when the settings say sending issues it.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Send))]
    public async Task<IActionResult> SendPost(InvoiceSendViewModel model, [FromServices] IInvoiceSender sender, [FromServices] ICommerceEmailSender emailSender)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(model.ItemId);

        if (invoice is null)
        {
            return NotFound();
        }

        // Sending a draft issues it, which needs its own permission.
        if (invoice.Status == InvoiceStatus.Draft && !await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.IssueInvoices))
        {
            return Forbid();
        }

        var result = await sender.SendAsync(new InvoiceSendRequest
        {
            InvoiceId = invoice.ItemId,
            To = model.To,
            Cc = Split(model.Cc),
            Bcc = Split(model.Bcc),
        }, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(nameof(Send), await PrepareSendAsync(model, result.Invoice ?? invoice, emailSender));
        }

        await _notifier.SuccessAsync(result.Issued
            ? H["Invoice {0} was issued and sent.", result.Invoice.Number]
            : H["Invoice {0} was sent.", result.Invoice.Number]);

        return RedirectToAction(nameof(Detail), new { itemId = invoice.ItemId });
    }

    /// <summary>
    /// Issues a draft: numbers it and posts what is owed to the ledger.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Issue(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.IssueInvoices))
        {
            return Forbid();
        }

        var result = await _commands.IssueAsync(itemId);

        return await CompleteAsync(result, itemId, result.Invoice is { } issued ? H["Invoice {0} was issued.", issued.Number] : null);
    }

    /// <summary>
    /// Submits a draft at or above the approval amount for approval.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices) &&
            !await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.IssueInvoices))
        {
            return Forbid();
        }

        return await CompleteAsync(await _commands.SubmitForApprovalAsync(itemId, User.Identity?.Name), itemId, H["The invoice was submitted for approval."]);
    }

    /// <summary>
    /// Approves an invoice waiting for approval, which issues it.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Approve(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ApproveInvoices))
        {
            return Forbid();
        }

        var result = await _commands.ApproveAsync(itemId, User.Identity?.Name);

        return await CompleteAsync(result, itemId, result.Invoice is { } issued ? H["Invoice {0} was approved and issued.", issued.Number] : null);
    }

    /// <summary>
    /// Rejects an invoice waiting for approval: it goes back to a draft.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Reject(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ApproveInvoices))
        {
            return Forbid();
        }

        return await CompleteAsync(await _commands.RejectAsync(itemId, User.Identity?.Name, reason), itemId, H["The invoice was rejected and is a draft again."]);
    }

    /// <summary>
    /// Drafts a revision of an open invoice and opens it in the editor.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Revise(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices) ||
            !await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.IssueInvoices))
        {
            return Forbid();
        }

        var result = await _commands.ReviseAsync(itemId);

        if (!result.Succeeded || result.Invoice is null)
        {
            return await CompleteAsync(result, itemId, null);
        }

        await _notifier.SuccessAsync(H["A revision was drafted. Change it and issue it: issuing it voids the invoice it revises."]);

        return RedirectToAction(nameof(Edit), new { itemId = result.Invoice.ItemId });
    }

    /// <summary>
    /// Voids an open invoice that nothing was paid on; its number is kept.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Void(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.VoidInvoices))
        {
            return Forbid();
        }

        return await CompleteAsync(await _commands.VoidAsync(itemId, reason), itemId, H["The invoice was voided."]);
    }

    /// <summary>
    /// Writes off what is still owed on an open invoice.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> WriteOff(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.VoidInvoices))
        {
            return Forbid();
        }

        return await CompleteAsync(await _commands.WriteOffAsync(itemId, reason), itemId, H["What was owed was written off."]);
    }

    /// <summary>
    /// Makes a written-off invoice owed again.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Reinstate(string itemId, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.VoidInvoices))
        {
            return Forbid();
        }

        return await CompleteAsync(await _commands.ReinstateAsync(itemId, reason), itemId, H["The invoice is owed again."]);
    }

    /// <summary>
    /// Deletes a draft. An issued invoice is never deleted.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageInvoices))
        {
            return Forbid();
        }

        var invoice = await FindAsync(itemId);

        if (invoice is null)
        {
            return NotFound();
        }

        if (!InvoiceLifecycle.CanDelete(invoice) || !await _invoiceManager.DeleteAsync(invoice))
        {
            await _notifier.WarningAsync(H["Only a draft can be deleted. Void an issued invoice instead."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        await _notifier.SuccessAsync(H["The draft was deleted."]);

        return RedirectToAction(nameof(Index));
    }

    // Writes the form onto the draft, works out its totals, validates and saves it, then stays in the editor, opens the
    // invoice's page, or issues it.
    private async Task<IActionResult> SaveAsync(Invoice invoice, InvoiceEditViewModel model, string submit, bool isNew)
    {
        var issue = string.Equals(submit, _submitIssue, StringComparison.Ordinal);

        if (issue && !await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.IssueInvoices))
        {
            return Forbid();
        }

        InvoiceDraftEditor.Apply(model, invoice);

        if (!string.IsNullOrEmpty(invoice.Currency) && invoice.Currency.Length != 3)
        {
            ModelState.AddModelError(nameof(model.Currency), S["The currency must be a three-letter code, such as USD."]);
        }

        if (!string.IsNullOrEmpty(model.BillTo) && invoice.BillTo.Party.IsEmpty)
        {
            ModelState.AddModelError(nameof(model.BillTo), S["Pick who the invoice is billed to from the list."]);
        }

        await InvoiceCalculation.ApplyAsync(_calculator, invoice, await GetSettingsAsync(), issueDate: null, HttpContext.RequestAborted);

        var validation = await _invoiceManager.ValidateAsync(invoice);

        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.MemberNames?.FirstOrDefault() ?? string.Empty, error.ErrorMessage);
        }

        if (!ModelState.IsValid)
        {
            InvoiceDraftEditor.Build(invoice, model);

            if (isNew)
            {
                model.ItemId = null;
            }

            return View("Edit", await PrepareAsync(model, invoice));
        }

        if (isNew)
        {
            await _invoiceManager.CreateAsync(invoice);
        }
        else
        {
            await _invoiceManager.UpdateAsync(invoice);
        }

        if (string.Equals(submit, _submitUpdate, StringComparison.Ordinal))
        {
            await _notifier.SuccessAsync(H["The draft was saved."]);

            return RedirectToAction(nameof(Edit), new { itemId = invoice.ItemId });
        }

        if (!issue)
        {
            await _notifier.SuccessAsync(H["The draft was saved."]);

            return RedirectToAction(nameof(Detail), new { itemId = invoice.ItemId });
        }

        // Issue reads the draft under its lock, so the draft is written first.
        await _session.SaveChangesAsync(HttpContext.RequestAborted);

        var result = await _commands.IssueAsync(invoice.ItemId);

        return await CompleteAsync(result, invoice.ItemId, result.Invoice is { } issued ? H["Invoice {0} was issued.", issued.Number] : null);
    }

    private async Task<InvoiceEditViewModel> PrepareAsync(InvoiceEditViewModel model, Invoice invoice)
    {
        model.PaymentTerms = [.. (await _paymentTerms.GetEnabledAsync()).Select(term => new SelectListItem(term.Name, term.ItemId, string.Equals(term.ItemId, model.PaymentTermId, StringComparison.Ordinal)))];
        model.LineKinds = BuildLineKinds();
        model.PartySearchEndpoint = Url.RouteUrl(InvoicePartySearchEndpoints.RouteName);
        model.CanIssue = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.IssueInvoices);
        model.Totals ??= invoice.Totals;
        model.CanSchedule = HttpContext.RequestServices.GetService<CrestApps.OrchardCore.Subscriptions.Services.IInstallmentPlanService>() is not null &&
            invoice.Kind == InvoiceKind.Standard;

        if (model.CanSchedule)
        {
            // Parts are added in the browser; a row left empty is not kept.
            model.Schedule.RemoveAll(row => !row.DueDate.HasValue && !row.Amount.HasValue && !row.Percent.HasValue);
        }

        if (PartyRef.TryParse(model.BillTo, out var party))
        {
            model.BillToName = await _parties.GetNameAsync(party) ?? model.BillTo;
        }

        return model;
    }

    private async Task<IActionResult> CompleteAsync(InvoiceCommandResult result, string itemId, LocalizedHtmlString success)
    {
        if (result.Invoice is null && result.Errors.Count == 0)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            if (success is not null)
            {
                await _notifier.SuccessAsync(success);
            }
        }
        else
        {
            foreach (var error in result.Errors)
            {
                await _notifier.WarningAsync(new LocalizedHtmlString(error, "{0}", false, error));
            }
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private async Task<InvoiceSendViewModel> PrepareSendAsync(InvoiceSendViewModel model, Invoice invoice, ICommerceEmailSender emailSender)
    {
        model.Number = invoice.Number;
        model.CanSend = emailSender.CanSend;
        model.IssuesFirst = invoice.Status == InvoiceStatus.Draft && (await GetSettingsAsync()).IssueOnSend;

        return model;
    }

    private static List<string> Split(string addresses)
        => string.IsNullOrWhiteSpace(addresses)
            ? []
            : [.. addresses.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private async Task<Invoice> FindAsync(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : await _invoiceManager.FindByIdAsync(itemId);

    private async Task<InvoicingSettings> GetSettingsAsync()
        => await _siteService.GetSettingsAsync<InvoicingSettings>();

    private static void AddFilterRouteValues(InvoicesAdminIndexOptions options, RouteValueDictionary values)
    {
        if (!string.IsNullOrEmpty(options.Search))
        {
            values.TryAdd(_optionsSearch, options.Search);
        }

        if (options.Status.HasValue)
        {
            values.TryAdd(_optionsStatus, options.Status.Value);
        }

        if (options.Settlement.HasValue)
        {
            values.TryAdd(_optionsSettlement, options.Settlement.Value);
        }
    }

    private List<SelectListItem> BuildStatusFilterItems(InvoiceStatus? selected)
        =>
        [
            new(S["Any status"], string.Empty, selected is null),
            new(S["Draft"], nameof(InvoiceStatus.Draft), selected == InvoiceStatus.Draft),
            new(S["Open"], nameof(InvoiceStatus.Open), selected == InvoiceStatus.Open),
            new(S["Written off"], nameof(InvoiceStatus.Uncollectible), selected == InvoiceStatus.Uncollectible),
            new(S["Void"], nameof(InvoiceStatus.Void), selected == InvoiceStatus.Void),
        ];

    private List<SelectListItem> BuildSettlementFilterItems(InvoiceSettlement? selected)
        =>
        [
            new(S["Any payment"], string.Empty, selected is null),
            new(S["Unpaid"], nameof(InvoiceSettlement.Unpaid), selected == InvoiceSettlement.Unpaid),
            new(S["Partly paid"], nameof(InvoiceSettlement.PartiallyPaid), selected == InvoiceSettlement.PartiallyPaid),
            new(S["Paid"], nameof(InvoiceSettlement.Paid), selected == InvoiceSettlement.Paid),
        ];

    private List<SelectListItem> BuildLineKinds()
        =>
        [
            new(S["Item"], nameof(DocumentLineKind.Item)),
            new(S["Service"], nameof(DocumentLineKind.Service)),
            new(S["Time"], nameof(DocumentLineKind.Time)),
            new(S["Expense"], nameof(DocumentLineKind.Expense)),
            new(S["Fee"], nameof(DocumentLineKind.Fee)),
            new(S["Deposit"], nameof(DocumentLineKind.Deposit)),
            new(S["Text"], nameof(DocumentLineKind.Text)),
            new(S["Section heading"], nameof(DocumentLineKind.Section)),
        ];
}
