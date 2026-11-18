using System.Security.Claims;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using OrchardCore;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.Api;

/// <summary>
/// Invoices over the commerce JSON API: staff and back-office systems list, find, draft, issue, send and void invoices,
/// record payments taken elsewhere and download the PDF, through the same commands as the admin; a signed-in customer
/// lists and reads their own.
/// </summary>
internal static class InvoicingApiEndpoints
{
    public const string Tag = "Invoices";

    // The problem code of a command the invoice's state refuses, such as issuing a void invoice.
    private const string InvalidState = "invalid-state";

    public static IEndpointRouteBuilder MapInvoicingApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapCommerceApi(Tag);

        group.MapGet("invoices", ListAsync)
            .WithName("ListInvoices")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists invoices, newest first: by status (draft, open, void, uncollectible), settlement (unpaid, partiallyPaid, paid), customer (a party such as User:42 or Account:7) or a search of number, customer and reference.");

        group.MapGet("invoices/{invoiceId}", GetAsync)
            .WithName("GetInvoice")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets an invoice with its lines, taxes, totals and what is paid and still due.");

        group.MapGet("invoices/by-number/{number}", GetByNumberAsync)
            .WithName("GetInvoiceByNumber")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets an issued invoice by its number.");

        group.MapPost("invoices", CreateAsync)
            .WithIdempotency()
            .WithName("CreateInvoice")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Drafts an invoice from its customer and lines, worked out by the store's tax and rounding; with issue true, issues it too.");

        group.MapPost("invoices/{invoiceId}/issue", IssueAsync)
            .WithIdempotency()
            .WithName("IssueInvoice")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Issues a draft: numbers it and posts what is owed to the ledger. Issuing again returns the issued invoice.");

        group.MapPost("invoices/{invoiceId}/send", SendAsync)
            .WithIdempotency()
            .WithName("SendInvoice")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("E-mails an issued invoice to its customer with its PDF and pay link.");

        group.MapPost("invoices/{invoiceId}/void", VoidAsync)
            .WithIdempotency()
            .WithName("VoidInvoice")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Voids an open invoice nothing was paid on, with a reason.");

        group.MapPost("invoices/{invoiceId}/payments", RecordPaymentAsync)
            .WithIdempotency()
            .WithName("RecordInvoicePayment")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Records a payment taken outside the site (a check, a bank transfer) on an issued invoice.");

        group.MapGet("invoices/{invoiceId}/pdf", PdfAsync)
            .WithName("GetInvoicePdf")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Downloads the invoice's PDF: the one archived at issue, or one printed now.");

        group.MapGet("me/invoices", ListMineAsync)
            .WithName("ListMyInvoices")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the signed-in customer's issued invoices and those of the accounts they pay for, newest first.");

        group.MapGet("me/invoices/{invoiceId}", GetMineAsync)
            .WithName("GetMyInvoice")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets one of the signed-in customer's invoices.");

        return routes;
    }

    internal static async Task<Results<Ok<ApiPage<ApiInvoiceSummary>>, ProblemHttpResult>> ListAsync(
        string status,
        string settlement,
        string billTo,
        string q,
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceManager invoices)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ViewInvoices))
        {
            return ApiProblems.Forbidden();
        }

        var query = new InvoiceQuery
        {
            Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            Status = Enum.TryParse<InvoiceStatus>(status, ignoreCase: true, out var parsedStatus) ? parsedStatus : null,
            Settlement = Enum.TryParse<InvoiceSettlement>(settlement, ignoreCase: true, out var parsedSettlement) ? parsedSettlement : null,
        };

        if (PartyRef.TryParse(billTo, out var party))
        {
            query.BillTo = [party];
            query.OnlyBillTo = true;
        }

        return TypedResults.Ok(await PageAsync(invoices, query, skip, take, httpContext.RequestAborted));
    }

    internal static async Task<Results<Ok<ApiInvoice>, ProblemHttpResult>> GetAsync(
        string invoiceId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceManager invoices)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ViewInvoices))
        {
            return ApiProblems.Forbidden();
        }

        return string.IsNullOrEmpty(invoiceId) || await invoices.FindByIdAsync(invoiceId, httpContext.RequestAborted) is not { } invoice
            ? ApiProblems.NotFound()
            : TypedResults.Ok(InvoicingApiMapper.ToInvoice(invoice));
    }

    internal static async Task<Results<Ok<ApiInvoice>, ProblemHttpResult>> GetByNumberAsync(
        string number,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceStore invoices)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ViewInvoices))
        {
            return ApiProblems.Forbidden();
        }

        return string.IsNullOrWhiteSpace(number) || await invoices.FindByNumberAsync(number.Trim(), httpContext.RequestAborted) is not { } invoice
            ? ApiProblems.NotFound()
            : TypedResults.Ok(InvoicingApiMapper.ToInvoice(invoice));
    }

    internal static async Task<Results<Created<ApiInvoice>, ValidationProblem, ProblemHttpResult>> CreateAsync(
        ApiInvoiceDraft draft,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceManager invoices,
        IInvoiceCommandService commands,
        IDocumentCalculator calculator,
        ISiteService siteService,
        global::YesSql.ISession session)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ManageInvoices) ||
            draft?.Issue == true && !await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.IssueInvoices))
        {
            return ApiProblems.Forbidden();
        }

        if (draft is null || draft.Lines is not { Count: > 0 })
        {
            return ApiProblems.Validation("lines", "An invoice needs at least one line.");
        }

        var settings = await siteService.GetSettingsAsync<InvoicingSettings>();
        var invoice = await invoices.NewAsync(cancellationToken: httpContext.RequestAborted);

        invoice.Currency = string.IsNullOrWhiteSpace(draft.Currency) ? settings.DefaultCurrency : draft.Currency.Trim().ToUpperInvariant();
        invoice.PricesIncludeTax = draft.PricesIncludeTax ?? settings.PricesIncludeTax;
        invoice.AllowPartialPayment = draft.AllowPartialPayment ?? settings.AllowPartialPayment;
        invoice.AutoCharge = draft.AutoCharge;
        invoice.IssueDate = draft.IssueDate;
        invoice.DueDate = draft.DueDate;
        invoice.PaymentTermId = draft.PaymentTermId;
        invoice.Reference = draft.Reference;
        invoice.PurchaseOrderNumber = draft.PurchaseOrderNumber;
        invoice.Notes = draft.Notes;
        invoice.Terms = draft.Terms;
        invoice.InternalNote = draft.InternalNote;
        invoice.SourceType = draft.SourceType;
        invoice.SourceId = draft.SourceId;
        invoice.BillTo = new DocumentParty
        {
            Party = PartyRef.TryParse(draft.BillTo?.Party, out var party) ? party : default,
            Contact = new ContactInfo { Name = draft.BillTo?.Name, Email = draft.BillTo?.Email },
        };

        foreach (var line in draft.Lines)
        {
            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = Enum.TryParse<DocumentLineKind>(line.Kind, ignoreCase: true, out var kind) ? kind : DocumentLineKind.Item,
                Sku = line.Sku,
                Name = line.Name,
                Description = line.Description,
                Quantity = line.Quantity ?? 1m,
                Unit = line.Unit,
                UnitPrice = line.UnitPrice,
                DiscountPercent = line.DiscountPercent,
                TaxCategoryCode = line.TaxCategoryCode,
            });
        }

        await InvoiceCalculation.ApplyAsync(calculator, invoice, settings, issueDate: null, httpContext.RequestAborted);

        var validation = await invoices.ValidateAsync(invoice, httpContext.RequestAborted);

        if (!validation.Succeeded)
        {
            return ApiProblems.Validation(validation.Errors
                .GroupBy(error => error.MemberNames?.FirstOrDefault() ?? string.Empty)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()));
        }

        await invoices.CreateAsync(invoice, httpContext.RequestAborted);

        if (draft.Issue)
        {
            // Issuing reads the draft under its lock, so the draft is written first.
            await session.SaveChangesAsync(httpContext.RequestAborted);

            var issued = await commands.IssueAsync(invoice.ItemId, httpContext.RequestAborted);

            if (!issued.Succeeded)
            {
                return ApiProblems.Validation("issue", string.Join(" ", issued.Errors));
            }

            invoice = issued.Invoice;
        }

        return TypedResults.Created($"{CommerceApi.RoutePrefix}/invoices/{invoice.ItemId}", InvoicingApiMapper.ToInvoice(invoice));
    }

    internal static async Task<Results<Ok<ApiInvoice>, ProblemHttpResult>> IssueAsync(
        string invoiceId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceCommandService commands)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.IssueInvoices))
        {
            return ApiProblems.Forbidden();
        }

        return Complete(await commands.IssueAsync(invoiceId, httpContext.RequestAborted));
    }

    internal static async Task<Results<Ok<ApiInvoice>, ProblemHttpResult>> SendAsync(
        string invoiceId,
        ApiInvoiceSendRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceSender sender)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ManageInvoices))
        {
            return ApiProblems.Forbidden();
        }

        var sent = await sender.SendAsync(new InvoiceSendRequest { InvoiceId = invoiceId, To = request?.To }, httpContext.RequestAborted);

        if (sent.Invoice is null && sent.Errors.Count > 0)
        {
            return ApiProblems.Conflict(InvalidState, string.Join(" ", sent.Errors));
        }

        return sent.Succeeded
            ? TypedResults.Ok(InvoicingApiMapper.ToInvoice(sent.Invoice))
            : ApiProblems.Conflict(InvalidState, string.Join(" ", sent.Errors));
    }

    internal static async Task<Results<Ok<ApiInvoice>, ProblemHttpResult>> VoidAsync(
        string invoiceId,
        ApiInvoiceReasonRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceCommandService commands)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.VoidInvoices))
        {
            return ApiProblems.Forbidden();
        }

        return Complete(await commands.VoidAsync(invoiceId, request?.Reason, httpContext.RequestAborted));
    }

    internal static async Task<Results<Ok<ApiInvoice>, ProblemHttpResult>> RecordPaymentAsync(
        string invoiceId,
        ApiInvoicePaymentRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceManager invoices,
        ITransactionCommandService ledger,
        InvoiceLedgerSync sync)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.RecordInvoicePayments))
        {
            return ApiProblems.Forbidden();
        }

        if (string.IsNullOrEmpty(invoiceId) || await invoices.FindByIdAsync(invoiceId, httpContext.RequestAborted) is not { } invoice)
        {
            return ApiProblems.NotFound();
        }

        if (invoice.Status != InvoiceStatus.Open || string.IsNullOrEmpty(invoice.TransactionId))
        {
            return ApiProblems.Conflict(InvalidState, "Only an open invoice that owes something takes a payment.");
        }

        if (request is null || request.Amount <= 0m)
        {
            return ApiProblems.Conflict(InvalidState, "Give the amount received, above zero.");
        }

        var paid = await ledger.RecordPaymentAsync(invoice.TransactionId, new TransactionPaymentRequest
        {
            Amount = request.Amount,
            PaymentMethod = request.Method,
            Reference = request.Reference,
            Note = request.Note,
        }, httpContext.RequestAborted);

        if (!paid.Succeeded)
        {
            return ApiProblems.Conflict(InvalidState, string.Join(" ", paid.Errors));
        }

        return TypedResults.Ok(InvoicingApiMapper.ToInvoice(await sync.SyncAsync(invoice, httpContext.RequestAborted) ?? invoice));
    }

    internal static async Task<Results<FileContentHttpResult, ProblemHttpResult>> PdfAsync(
        string invoiceId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceManager invoices,
        IInvoicePdfService pdfService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ViewInvoices))
        {
            return ApiProblems.Forbidden();
        }

        if (string.IsNullOrEmpty(invoiceId) || await invoices.FindByIdAsync(invoiceId, httpContext.RequestAborted) is not { } invoice)
        {
            return ApiProblems.NotFound();
        }

        var pdf = await pdfService.GetAsync(invoice, httpContext.RequestAborted);

        return TypedResults.File(pdf.Content, "application/pdf", pdf.FileName);
    }

    internal static async Task<Results<Ok<ApiPage<ApiInvoiceSummary>>, ProblemHttpResult>> ListMineAsync(
        int? skip,
        int? take,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceVisibilityService visibility,
        IInvoiceManager invoices)
    {
        if (UserId(httpContext.User) is not { } userId || !await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ViewOwnInvoices))
        {
            return ApiProblems.Forbidden();
        }

        var query = await visibility.CreateQueryAsync(userId, httpContext.RequestAborted);

        return TypedResults.Ok(await PageAsync(invoices, query, skip, take, httpContext.RequestAborted));
    }

    internal static async Task<Results<Ok<ApiInvoice>, ProblemHttpResult>> GetMineAsync(
        string invoiceId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IInvoiceVisibilityService visibility,
        IInvoiceManager invoices)
    {
        if (UserId(httpContext.User) is not { } userId || !await authorizationService.AuthorizeAsync(httpContext.User, InvoicingPermissions.ViewOwnInvoices))
        {
            return ApiProblems.Forbidden();
        }

        // Someone else's invoice, or a draft, is not found rather than forbidden: its existence is not disclosed.
        return string.IsNullOrEmpty(invoiceId) ||
            await invoices.FindByIdAsync(invoiceId, httpContext.RequestAborted) is not { } invoice ||
            invoice.Status == InvoiceStatus.Draft ||
            !await visibility.CanViewAsync(invoice, userId, httpContext.RequestAborted)
            ? ApiProblems.NotFound()
            : TypedResults.Ok(InvoicingApiMapper.ToInvoice(invoice, forCustomer: true));
    }

    private static async Task<ApiPage<ApiInvoiceSummary>> PageAsync(IInvoiceManager invoices, InvoiceQuery query, int? skip, int? take, CancellationToken cancellationToken)
    {
        var pageSkip = ApiPage<ApiInvoiceSummary>.ClampSkip(skip);
        var pageTake = ApiPage<ApiInvoiceSummary>.ClampTake(take);

        // The manager pages by page number; a skip that is not a whole number of pages starts at the page it falls in.
        var result = await invoices.PageAsync((pageSkip / pageTake) + 1, pageTake, query, cancellationToken);

        return new ApiPage<ApiInvoiceSummary>([.. result.Entries.Select(InvoicingApiMapper.ToSummary)], result.Count, pageSkip, pageTake);
    }

    private static Results<Ok<ApiInvoice>, ProblemHttpResult> Complete(InvoiceCommandResult result)
    {
        if (result.Invoice is null)
        {
            return result.Errors.Count == 0 ? ApiProblems.NotFound() : ApiProblems.Conflict(InvalidState, string.Join(" ", result.Errors));
        }

        return result.Succeeded
            ? TypedResults.Ok(InvoicingApiMapper.ToInvoice(result.Invoice))
            : ApiProblems.Conflict(InvalidState, string.Join(" ", result.Errors));
    }

    private static string UserId(ClaimsPrincipal user)
        => user?.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id ? id : null;
}
