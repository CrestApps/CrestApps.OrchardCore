using System.ComponentModel.DataAnnotations;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Core.Handlers;

/// <summary>
/// Audits invoices saved through <c>IInvoiceManager</c>, refuses lines out of range on a draft, and refuses any change to an
/// invoice that is no longer a draft: after issue only the invoice commands change it.
/// </summary>
public sealed class InvoiceHandler : AuditedCatalogEntryHandlerBase<Invoice>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The accessor for the user recorded as the author.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public InvoiceHandler(
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        IStringLocalizer<InvoiceHandler> stringLocalizer)
        : base(httpContextAccessor, clock)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task ValidatingAsync(ValidatingContext<Invoice> context, CancellationToken cancellationToken = default)
    {
        var invoice = context.Model;

        if (invoice.Status != InvoiceStatus.Draft || !string.IsNullOrEmpty(invoice.Number) || invoice.IssuedUtc.HasValue)
        {
            context.Result.Fail(new ValidationResult(S["An issued invoice can't be changed. Correct it with a credit note."]));

            return Task.CompletedTask;
        }

        foreach (var error in InvoiceIssueRules.ValidateLines(invoice))
        {
            context.Result.Fail(new ValidationResult(InvoiceIssueMessages.Describe(error, invoice, S), [nameof(Invoice.Lines)]));
        }

        return Task.CompletedTask;
    }
}
