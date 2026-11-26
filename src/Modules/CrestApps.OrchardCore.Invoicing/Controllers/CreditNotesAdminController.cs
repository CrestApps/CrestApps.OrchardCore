using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Invoicing.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Settings;
using YesSql;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Invoicing.Controllers;

/// <summary>
/// Credit notes in the admin: drafted from an invoice's page, edited (which lines, how much, why, and how the credit is
/// given), issued and deleted while a draft.
/// </summary>
[Admin("credit-notes/{action}/{itemId?}", "CreditNotes{action}")]
public sealed class CreditNotesAdminController : Controller
{
    private const string _submitIssue = "issue";
    private const string _submitUpdate = "update";

    private readonly ICreditNoteStore _creditNotes;
    private readonly IInvoiceStore _invoices;
    private readonly ICreditNoteCommandService _commands;
    private readonly IDocumentCalculator _calculator;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<CreditNoteEditViewModel> _editorManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly ISession _session;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CreditNotesAdminController(
        ICreditNoteStore creditNotes,
        IInvoiceStore invoices,
        ICreditNoteCommandService commands,
        IDocumentCalculator calculator,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        IDisplayManager<CreditNoteEditViewModel> editorManager,
        IUpdateModelAccessor updateModelAccessor,
        ISession session,
        INotifier notifier,
        IHtmlLocalizer<CreditNotesAdminController> htmlLocalizer,
        IStringLocalizer<CreditNotesAdminController> stringLocalizer)
    {
        _creditNotes = creditNotes;
        _invoices = invoices;
        _commands = commands;
        _calculator = calculator;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _editorManager = editorManager;
        _updateModelAccessor = updateModelAccessor;
        _session = session;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Drafts a credit note for an invoice, or opens the draft already started, and opens it in the editor.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(string invoiceId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageCreditNotes))
        {
            return Forbid();
        }

        var result = await _commands.CreateDraftAsync(invoiceId, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            await WarnAsync(result.Errors);

            return RedirectToAction(nameof(InvoicesAdminController.Detail), "InvoicesAdmin", new { itemId = invoiceId });
        }

        return RedirectToAction(nameof(Edit), new { itemId = result.CreditNote.ItemId });
    }

    /// <summary>
    /// Opens a draft credit note in the editor. An issued one opens its page.
    /// </summary>
    public async Task<IActionResult> Edit(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageCreditNotes))
        {
            return Forbid();
        }

        var creditNote = await FindAsync(itemId);

        if (creditNote is null)
        {
            return NotFound();
        }

        if (!CreditNoteLifecycle.CanEdit(creditNote))
        {
            return RedirectToAction(nameof(Detail), new { itemId });
        }

        var model = new CreditNoteEditViewModel();
        InvoiceDraftEditor.Build(creditNote, model);

        return View(await PrepareAsync(model, creditNote));
    }

    /// <summary>
    /// Saves a draft credit note, and issues it when asked.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(string itemId, CreditNoteEditViewModel model, string submit)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageCreditNotes))
        {
            return Forbid();
        }

        var creditNote = await FindAsync(itemId);

        if (creditNote is null)
        {
            return NotFound();
        }

        if (!CreditNoteLifecycle.CanEdit(creditNote))
        {
            return RedirectToAction(nameof(Detail), new { itemId });
        }

        model.Editor = await _editorManager.UpdateEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        InvoiceDraftEditor.Apply(model, creditNote);

        await InvoiceCalculation.ApplyAsync(_calculator, creditNote, await _siteService.GetSettingsAsync<InvoicingSettings>(), issueDate: null, HttpContext.RequestAborted);

        foreach (var error in InvoiceIssueRules.ValidateLines(creditNote))
        {
            ModelState.AddModelError(string.Empty, InvoiceIssueMessages.Describe(error, creditNote, S));
        }

        if (!ModelState.IsValid)
        {
            InvoiceDraftEditor.Build(creditNote, model);

            return View("Edit", await PrepareAsync(model, creditNote));
        }

        await _creditNotes.UpdateAsync(creditNote, HttpContext.RequestAborted);

        if (string.Equals(submit, _submitIssue, StringComparison.Ordinal))
        {
            // Issue reads the draft under the invoice's lock, so the draft is written first.
            await _session.SaveChangesAsync(HttpContext.RequestAborted);

            return await IssueAsync(itemId);
        }

        await _notifier.SuccessAsync(H["The draft credit note was saved."]);

        return string.Equals(submit, _submitUpdate, StringComparison.Ordinal)
            ? RedirectToAction(nameof(Edit), new { itemId })
            : RedirectToAction(nameof(Detail), new { itemId });
    }

    /// <summary>
    /// Shows a credit note.
    /// </summary>
    public async Task<IActionResult> Detail(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var creditNote = await FindAsync(itemId);

        if (creditNote is null)
        {
            return NotFound();
        }

        var canManage = await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageCreditNotes);

        return View(new CreditNoteDetailViewModel
        {
            CreditNote = creditNote,
            CanEdit = canManage && CreditNoteLifecycle.CanEdit(creditNote),
            CanIssue = canManage && creditNote.Status == CreditNoteStatus.Draft,
            CanViewInvoice = !string.IsNullOrEmpty(creditNote.InvoiceId),
        });
    }

    /// <summary>
    /// Shows the credit note as the customer gets it, to print.
    /// </summary>
    public async Task<IActionResult> Document(string itemId, [FromServices] IInvoiceDocumentBuilder builder)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var creditNote = await FindAsync(itemId);

        if (creditNote is null)
        {
            return NotFound();
        }

        return View("~/Areas/CrestApps.OrchardCore.Invoicing/Views/InvoicesAdmin/Document.cshtml", new InvoiceDocumentPageViewModel
        {
            Source = creditNote,
            Document = await builder.BuildAsync(creditNote, HttpContext.RequestAborted),
            PdfUrl = Url.Action(nameof(Pdf), new { itemId }),
            BackUrl = Url.Action(nameof(Detail), new { itemId }),
        });
    }

    /// <summary>
    /// Downloads the credit note's PDF.
    /// </summary>
    public async Task<IActionResult> Pdf(string itemId, [FromServices] IInvoicePdfService pdfService)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ViewInvoices))
        {
            return Forbid();
        }

        var creditNote = await FindAsync(itemId);

        if (creditNote is null)
        {
            return NotFound();
        }

        var pdf = await pdfService.GetAsync(creditNote, HttpContext.RequestAborted);

        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>
    /// Issues a draft credit note.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Issue(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageCreditNotes))
        {
            return Forbid();
        }

        return await IssueAsync(itemId);
    }

    /// <summary>
    /// Deletes a draft credit note. An issued one is never deleted.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(string itemId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, InvoicingPermissions.ManageCreditNotes))
        {
            return Forbid();
        }

        var creditNote = await FindAsync(itemId);

        if (creditNote is null)
        {
            return NotFound();
        }

        if (creditNote.Status != CreditNoteStatus.Draft || !await _creditNotes.DeleteAsync(creditNote, HttpContext.RequestAborted))
        {
            await _notifier.WarningAsync(H["Only a draft credit note can be deleted."]);

            return RedirectToAction(nameof(Detail), new { itemId });
        }

        await _notifier.SuccessAsync(H["The draft credit note was deleted."]);

        return RedirectToAction(nameof(InvoicesAdminController.Detail), "InvoicesAdmin", new { itemId = creditNote.InvoiceId });
    }

    private async Task<IActionResult> IssueAsync(string itemId)
    {
        var result = await _commands.IssueAsync(itemId, HttpContext.RequestAborted);

        if (result.CreditNote is null)
        {
            return NotFound();
        }

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(H["Credit note {0} was issued.", result.CreditNote.Number]);
        }
        else
        {
            await WarnAsync(result.Errors);
        }

        return RedirectToAction(nameof(Detail), new { itemId });
    }

    private async Task<CreditNoteEditViewModel> PrepareAsync(CreditNoteEditViewModel model, CreditNote creditNote)
    {
        var credit = HttpContext.RequestServices.GetService<ICustomerCreditService>();

        model.CanIssue = true;
        model.Dispositions =
        [
            new(S["Lower what is owed on the invoice"], nameof(CreditNoteDisposition.ReduceBalance), model.Disposition == CreditNoteDisposition.ReduceBalance),
            new(S["Refund what was paid"], nameof(CreditNoteDisposition.Refund), model.Disposition == CreditNoteDisposition.Refund),
        ];

        if (credit is not null)
        {
            model.Dispositions.Add(new SelectListItem(S["Keep it as the customer's credit"], nameof(CreditNoteDisposition.CustomerCredit), model.Disposition == CreditNoteDisposition.CustomerCredit));
        }

        if (await _invoices.FindByIdAsync(creditNote.InvoiceId, HttpContext.RequestAborted) is { } invoice)
        {
            var others = await _creditNotes.ListByInvoiceAsync(invoice.ItemId, HttpContext.RequestAborted);

            model.Creditable = CreditNoteRules.GetCreditable(invoice, others.Where(note => !string.Equals(note.ItemId, creditNote.ItemId, StringComparison.Ordinal))).Total;
        }

        model.Editor = await _editorManager.BuildEditorAsync(model, _updateModelAccessor.ModelUpdater, isNew: false);

        return model;
    }

    private async Task<CreditNote> FindAsync(string itemId)
        => string.IsNullOrEmpty(itemId) ? null : await _creditNotes.FindByIdAsync(itemId, HttpContext.RequestAborted);

    private async Task WarnAsync(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            await _notifier.WarningAsync(new LocalizedHtmlString(error, HtmlEncoder.Default.Encode(error)));
        }
    }
}
