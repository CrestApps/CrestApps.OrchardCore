using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Invoicing.Core;
using CrestApps.OrchardCore.Invoicing.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// Prepares a credit note for an issued invoice as the credit note pages do, without issuing it (the owner's review, F23).
/// With only the invoice it lists what is still creditable on each line. With the lines to credit (or <c>creditAll</c>)
/// and the disposition, it starts the draft through <see cref="ICreditNoteCommandService.CreateDraftAsync"/> (the
/// invoice's <b>Credit</b> button), keeps the lines asked for as the draft editor would, works it out with the calculator,
/// checks it with the rules <see cref="ICreditNoteCommandService.IssueAsync"/> applies (what is left to credit on each line
/// and on the invoice, and what the ledger allows for the disposition) and saves it. A draft that breaks a rule is deleted
/// again. It returns a token, bound to the caller and to the stored draft, that <c>issueCreditNote</c> requires.
/// </summary>
internal sealed class PreviewCreditNoteTool : CreditNoteAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = PreviewToolName;

    private const string Refused = "Nothing was prepared";

    private const int MaxLines = 100;

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "invoice": {
          "type": "string",
          "description": "The invoice's number, such as INV-1001, or its id from findInvoices."
        },
        "currency": {
          "type": "string",
          "description": "The invoice's currency, such as USD. Required with lines or creditAll."
        },
        "lines": {
          "type": "array",
          "description": "The invoice lines to credit, each with quantity or amount. Line ids come from calling this tool with only the invoice.",
          "items": {
            "type": "object",
            "properties": {
              "invoiceLineId": { "type": "string", "description": "The invoice line's id." },
              "quantity": { "type": "number", "description": "How many units to credit at the line's own price, more than zero and at most the line's quantity. Only for a line nothing was credited on yet." },
              "amount": { "type": "number", "description": "How much of the line's total, tax included, to credit, more than zero and at most what is left to credit on it." }
            },
            "required": ["invoiceLineId"],
            "additionalProperties": false
          }
        },
        "creditAll": {
          "type": "boolean",
          "description": "Set to true to credit everything still creditable on the invoice, instead of lines."
        },
        "disposition": {
          "type": "string",
          "enum": ["reduceBalance", "refund", "customerCredit"],
          "description": "What the credit does: lower what the invoice still owes, refund what was paid through the payment provider, or keep it as the customer's credit. Required with lines or creditAll."
        },
        "reason": {
          "type": "string",
          "description": "Why the invoice is credited, printed on the credit note. Never a card or bank account number."
        },
        "replaceDraft": {
          "type": "string",
          "description": "The id of a draft credit note already started on this invoice, to replace its lines, reason and disposition with these. It is changed only when the new credit passes every check. Only when the user says so; a draft your own earlier preview started is replaced without it."
        }
      },
      "required": ["invoice"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Prepares a credit note for an issued invoice without issuing it. With only the invoice it lists what is still creditable on each line and the credit notes already issued. With the lines to credit (or creditAll), the currency and the disposition (reduceBalance, refund or customerCredit), it saves the credit note as a draft and returns its lines, tax, total and effect, with a previewToken that issueCreditNote requires. Show the result to the user before issuing. The token works once, only for you and this draft, and expires after 10 minutes.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, caller, InvoicingPermissions.ManageCreditNotes))
        {
            return NotAllowedMessage;
        }

        var (found, notFound) = await FindInvoiceAsync(services, Read(arguments, "invoice"), cancellationToken);

        if (found is null)
        {
            return $"{Refused}: {notFound}";
        }

        var ledger = await ReadLedgerAsync(services, found, Refused, cancellationToken);

        if (ledger.Error is not null)
        {
            return ledger.Error;
        }

        var invoice = ledger.Invoice;
        var store = services.GetRequiredService<ICreditNoteStore>();
        var notes = await store.ListByInvoiceAsync(invoice.ItemId, cancellationToken);
        var creditable = CreditNoteRules.GetCreditable(invoice, notes);
        var creditAll = ReadBool(arguments, "creditAll") == true;
        var requested = InvoicingToolArguments.ReadArray(arguments, "lines");

        if (!creditAll && requested is not { Count: > 0 })
        {
            return DescribeCreditable(invoice, ledger, creditable, notes);
        }

        if (creditAll && requested is { Count: > 0 })
        {
            return $"{Refused}: give either lines or creditAll, not both.";
        }

        // The money rules (F23): the currency is always given and is the invoice's.
        var currency = Read(arguments, "currency");

        if (!string.Equals(currency, invoice.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"{Refused}: invoice {invoice.Number} is in {invoice.Currency}; give currency {invoice.Currency}.";
        }

        if (ParseDisposition(Read(arguments, "disposition")) is not { } disposition)
        {
            return $"{Refused}: give the disposition: reduceBalance, refund or customerCredit.";
        }

        var reason = Read(arguments, "reason");

        if (reason?.Length > MaxReasonLength)
        {
            return $"{Refused}: the reason can be {MaxReasonLength} characters at most.";
        }

        if (RefuseAccountNumbers(Refused, reason) is { } accountNumber)
        {
            return accountNumber;
        }

        if (disposition == CreditNoteDisposition.CustomerCredit &&
            (services.GetService<ICustomerCreditService>() is not { } credit || !credit.CanHold(invoice.BillTo?.Party ?? default)))
        {
            return $"{Refused}: the customer can't hold credit (the Store Credit feature is off, or the invoice is billed to someone without an account). Choose another disposition.";
        }

        List<LineRequest> lines = null;

        if (!creditAll)
        {
            var (read, linesError) = ReadLines(requested, invoice, creditable, currency);

            if (linesError is not null)
            {
                return linesError;
            }

            lines = read;
        }

        if (creditable.Total <= 0m)
        {
            return $"{Refused}: everything on invoice {invoice.Number} has been credited already.";
        }

        // A draft the caller's own earlier preview started is replaced by previewing again, as the preview's message tells
        // the model to do in a later message. Any other draft on the invoice is someone's work: it is replaced only when
        // the user says so.
        var replaceDraft = Read(arguments, "replaceDraft");
        var existing = notes.FirstOrDefault(note => note.Status == CreditNoteStatus.Draft);

        if (existing is not null && replaceDraft is null && await IsPreviewDraftAsync(services, caller, existing.ItemId, cancellationToken))
        {
            replaceDraft = existing.ItemId;
        }

        if (existing is not null && !string.Equals(existing.ItemId, replaceDraft, StringComparison.Ordinal))
        {
            return Json(new
            {
                draftId = existing.ItemId,
                draftTotal = existing.Totals?.Total ?? 0m,
                currency = existing.Currency,
                adminUrl = CreditNoteAdminPath(services, existing.ItemId),
                message = $"{Refused}: invoice {invoice.Number} already has a draft credit note ({existing.ItemId}), started on its page or by another user. Finish or delete it there, or, only if the user says so, call {PreviewToolName} again with replaceDraft set to its id to replace its lines, reason and disposition with these.",
            });
        }

        if (existing is null && replaceDraft is not null)
        {
            return $"{Refused}: invoice {invoice.Number} has no draft credit note '{replaceDraft}'. Leave out replaceDraft.";
        }

        if (await WriteEarlierWorkAsync(services, cancellationToken) is { } earlier)
        {
            return earlier;
        }

        // A new draft is started through the invoice's Credit action; a draft the user asked to replace is changed in
        // place, and only once the new credit passes every check, so a refused replacement leaves it as it was.
        CreditNote draft;

        if (existing is not null)
        {
            draft = existing;
        }
        else
        {
            var created = await services.GetRequiredService<ICreditNoteCommandService>().CreateDraftAsync(invoice.ItemId, cancellationToken);

            if (!created.Succeeded || created.CreditNote is null)
            {
                return Json(new
                {
                    invoiceId = invoice.ItemId,
                    invoiceNumber = invoice.Number,
                    errors = created.Errors,
                    message = $"{Refused}; read the errors.",
                });
            }

            if (created.AlreadyDone)
            {
                return $"{Refused}: someone started a draft credit note on invoice {invoice.Number} at the same moment ({created.CreditNote.ItemId}). Read the invoice again in your next message.";
            }

            draft = created.CreditNote;
            invoice = created.Invoice ?? invoice;
        }

        // The credit is worked out and checked on a copy of a replaced draft first; a new draft is this call's own.
        var settings = await services.GetRequiredService<ISiteService>().GetSettingsAsync<InvoicingSettings>();
        var calculator = services.GetRequiredService<IDocumentCalculator>();
        var candidate = existing is null ? draft : Copy(draft);

        await ApplyCreditAsync(calculator, settings, candidate, invoice, creditable, lines, reason, disposition, cancellationToken);

        var localizer = services.GetRequiredService<IStringLocalizer<PreviewCreditNoteTool>>();
        var problems = new List<string>();

        problems.AddRange(InvoiceIssueRules.ValidateLines(candidate).Select(error => InvoiceIssueMessages.Describe(error, candidate, localizer)));
        problems.AddRange(CreditNoteRules.Validate(invoice, notes, candidate).Select(error => DescribeRule(error, invoice, candidate)));

        var total = candidate.Totals?.Total ?? 0m;

        // Everything still creditable needs creditAll (F23); lines may never add up to it without the flag.
        if (problems.Count == 0 && !creditAll &&
            CheckAmount(total, currency, invoice.Currency, creditable.Total, all: false, Refused, $"still creditable on invoice {invoice.Number}", "creditAll") is { } amountError)
        {
            problems.Add(amountError);
        }

        var limit = CreditNoteRules.GetDispositionLimit(disposition, ledger.Transaction);

        if (DescribeDispositionLimit(disposition, limit, total, invoice.Currency) is { } overLimit)
        {
            problems.Add(overLimit);
        }

        if (problems.Count > 0)
        {
            if (existing is null)
            {
                return await DiscardAsync(services, store, draft, problems, invoice, cancellationToken);
            }

            return Json(new
            {
                invoiceId = invoice.ItemId,
                invoiceNumber = invoice.Number,
                errors = problems.ToArray(),
                message = $"{Refused}; read the errors. The draft credit note {existing.ItemId} was left as it was.",
            });
        }

        if (existing is not null)
        {
            await ApplyCreditAsync(calculator, settings, draft, invoice, creditable, lines, reason, disposition, cancellationToken);
        }

        await store.UpdateAsync(draft, cancellationToken);

        if (!await SaveAsync(services, cancellationToken))
        {
            return $"{Refused}: the invoice or its credit notes changed at the same moment. Read the invoice again in your next message.";
        }

        await RememberPreviewDraftAsync(services, caller, draft.ItemId, cancellationToken);

        var (token, expiresAt, tokenError) = await IssuePreviewTokenAsync(services, caller, CreditNoteFingerprint(draft, limit), cancellationToken);

        return Json(new
        {
            previewToken = token,
            expiresAt,
            message = tokenError is not null
                ? $"The credit note was saved as a draft, but {tokenError} Nothing was issued."
                : $"Nothing was issued. If the user already agreed to exactly this, call issueCreditNote now, in this reply; otherwise show it to the user and ask. In a later message, preview again for a new token. Call issueCreditNote with creditNoteId {draft.ItemId}, total {total.ToString(CultureInfo.InvariantCulture)}, currency {draft.Currency}, disposition {ToCamel(draft.Disposition.ToString())} and this previewToken, before it expires. Until it is issued or deleted, the draft stays on the invoice's page.",
            invoice = new
            {
                id = invoice.ItemId,
                number = invoice.Number,
                customer = invoice.BillTo?.Contact?.Name,
                currency = invoice.Currency,
                total = invoice.Totals?.Total ?? 0m,
                amountDue = invoice.AmountDue,
            },
            creditNote = new
            {
                id = draft.ItemId,
                status = ToCamel(draft.Status.ToString()),
                lines = DescribeLines(draft),
                subtotal = draft.Totals?.Subtotal ?? 0m,
                tax = draft.Totals?.TaxTotal ?? 0m,
                total,
                currency = draft.Currency,
                disposition = ToCamel(draft.Disposition.ToString()),
                reason = draft.Reason,
                adminUrl = CreditNoteAdminPath(services, draft.ItemId),
            },
            effect = DescribeEffect(draft, invoice, ledger.Transaction),
        });
    }

    // As the credit note editor: each line credits an invoice line, copied as it is (a quantity at the line's own price)
    // or as part of its total at its own taxes, the share of its net the credit note service uses for a part; with
    // creditAll, everything left on every line, as the invoice's Credit action drafts it.
    private static async Task ApplyCreditAsync(
        IDocumentCalculator calculator,
        InvoicingSettings settings,
        CreditNote creditNote,
        Invoice invoice,
        CreditableAmounts creditable,
        List<LineRequest> lines,
        string reason,
        CreditNoteDisposition disposition,
        CancellationToken cancellationToken)
    {
        var credited = new List<DocumentLine>();

        if (lines is null)
        {
            foreach (var line in invoice.Lines.Where(line => line.HasAmount && !string.IsNullOrEmpty(line.Id)))
            {
                if (creditable.Lines.TryGetValue(line.Id, out var left) && left > 0m)
                {
                    credited.Add(CreditNoteLines.ForLine(line, left, invoice.Currency));
                }
            }
        }
        else
        {
            foreach (var line in lines)
            {
                if (line.Quantity is { } quantity)
                {
                    // Nothing was credited on the line yet (ReadLines checks it), so it is copied whole.
                    var copy = CreditNoteLines.ForLine(line.InvoiceLine, line.InvoiceLine.Total, invoice.Currency);

                    copy.Quantity = quantity;
                    credited.Add(copy);
                }
                else
                {
                    credited.Add(CreditNoteLines.ForPartOf(line.InvoiceLine, line.InvoiceLine.Net * line.Amount.Value / line.InvoiceLine.Total, invoice.Currency));
                }
            }
        }

        creditNote.Lines = credited;
        creditNote.Reason = reason;
        creditNote.Disposition = disposition;

        await InvoiceCalculation.ApplyAsync(calculator, creditNote, settings, issueDate: null, cancellationToken);
    }

    // A working copy of a stored draft, so a refused replacement never changes the draft the session tracks.
    private static CreditNote Copy(CreditNote creditNote)
        => JsonSerializer.Deserialize<CreditNote>(JsonSerializer.Serialize(creditNote, JOptions.Default), JOptions.Default);

    private string DescribeCreditable(Invoice invoice, InvoiceLedgerBalance ledger, CreditableAmounts creditable, IReadOnlyList<CreditNote> notes)
        => Json(new
        {
            invoice = new
            {
                id = invoice.ItemId,
                number = invoice.Number,
                customer = invoice.BillTo?.Contact?.Name,
                status = ToCamel(invoice.Status.ToString()),
                currency = invoice.Currency,
                total = invoice.Totals?.Total ?? 0m,
                amountDue = invoice.AmountDue,
            },
            ledger = ledger.Transaction is { } transaction
                ? new
                {
                    owed = transaction.OutstandingAmount,
                    paid = transaction.AmountPaid,
                    refunded = transaction.AmountRefunded,
                }
                : null,
            creditable = new
            {
                total = creditable.Total,
                lines = invoice.Lines
                    .Where(line => line.HasAmount && !string.IsNullOrEmpty(line.Id))
                    .Select(line => new
                    {
                        invoiceLineId = line.Id,
                        name = line.Name,
                        quantity = line.Quantity,
                        unitPrice = line.UnitPrice,
                        total = line.Total,
                        leftToCredit = creditable.Lines.TryGetValue(line.Id, out var left) ? left : 0m,
                    })
                    .ToArray(),
            },
            creditNotes = notes.Select(note => new
            {
                id = note.ItemId,
                number = note.Number,
                status = ToCamel(note.Status.ToString()),
                disposition = ToCamel(note.Disposition.ToString()),
                total = note.Totals?.Total ?? 0m,
            }).ToArray(),
            message = $"Nothing was prepared. Only an issued invoice that is open or uncollectible, and not paid by a payment schedule, can be credited. To prepare a credit note, call {Name} again with lines (invoiceLineId with quantity or amount) or creditAll, the currency and the disposition.",
        });

    private static async Task<string> DiscardAsync(IServiceProvider services, ICreditNoteStore store, CreditNote draft, IEnumerable<string> problems, Invoice invoice, CancellationToken cancellationToken)
    {
        // The draft this call started is not kept: it breaks a rule the issue would refuse.
        await store.DeleteAsync(draft, cancellationToken);
        await SaveAsync(services, cancellationToken);

        return Json(new
        {
            invoiceId = invoice.ItemId,
            invoiceNumber = invoice.Number,
            errors = problems.ToArray(),
            message = $"{Refused}; read the errors. No draft was kept.",
        });
    }

    private static (List<LineRequest> Lines, string Error) ReadLines(IReadOnlyList<JsonElement> items, Invoice invoice, CreditableAmounts creditable, string currency)
    {
        if (items.Count > MaxLines)
        {
            return (null, $"{Refused}: a credit note takes at most {MaxLines} lines from a chat.");
        }

        var lines = new List<LineRequest>();

        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("invoiceLineId", out var idValue) || idValue.ValueKind != JsonValueKind.String)
            {
                return (null, $"{Refused}: every line needs an invoiceLineId, with quantity or amount.");
            }

            var id = idValue.GetString();
            var invoiceLine = invoice.Lines.FirstOrDefault(line => line.HasAmount && string.Equals(line.Id, id, StringComparison.Ordinal));

            if (invoiceLine is null)
            {
                return (null, $"{Refused}: invoice {invoice.Number} has no line '{id}' with an amount. Call {PreviewToolName} with only the invoice to list its lines.");
            }

            if (lines.Any(line => line.InvoiceLine == invoiceLine))
            {
                return (null, $"{Refused}: \"{invoiceLine.Name}\" is given twice.");
            }

            var hasQuantity = item.TryGetProperty("quantity", out var quantityValue) && quantityValue.ValueKind != JsonValueKind.Null;
            var hasAmount = item.TryGetProperty("amount", out var amountValue) && amountValue.ValueKind != JsonValueKind.Null;

            if (hasQuantity == hasAmount)
            {
                return (null, $"{Refused}: give \"{invoiceLine.Name}\" either a quantity or an amount.");
            }

            var left = creditable.Lines.TryGetValue(invoiceLine.Id, out var remaining) ? remaining : 0m;

            if (left <= 0m)
            {
                return (null, $"{Refused}: \"{invoiceLine.Name}\" was credited in full already.");
            }

            if (hasQuantity)
            {
                if (!InvoicingToolArguments.TryGetDecimal(quantityValue, out var quantity) || quantity <= 0m || quantity > invoiceLine.Quantity)
                {
                    return (null, $"{Refused}: the quantity of \"{invoiceLine.Name}\" must be more than zero and at most {invoiceLine.Quantity}.");
                }

                // Part of the line was credited already, so the draft carries what is left as one amount: credit an amount.
                if (left < invoiceLine.Total)
                {
                    return (null, $"{Refused}: part of \"{invoiceLine.Name}\" was credited already, so only an amount can be credited on it now, at most {Format(left, currency)}.");
                }

                lines.Add(new LineRequest(invoiceLine, quantity, null));

                continue;
            }

            if (!InvoicingToolArguments.TryGetDecimal(amountValue, out var amount) || amount <= 0m)
            {
                return (null, $"{Refused}: the amount of \"{invoiceLine.Name}\" must be more than zero.");
            }

            if (CurrencyScale.Round(amount, invoice.Currency) != amount)
            {
                return (null, $"{Refused}: the amount of \"{invoiceLine.Name}\" has more decimal places than {invoice.Currency} allows.");
            }

            if (amount > left)
            {
                return (null, $"{Refused}: \"{invoiceLine.Name}\" can be credited by {Format(left, currency)} at most.");
            }

            if (invoiceLine.Total == 0m)
            {
                return (null, $"{Refused}: \"{invoiceLine.Name}\" has no total to credit part of.");
            }

            lines.Add(new LineRequest(invoiceLine, null, amount));
        }

        return (lines, null);
    }

    private sealed record LineRequest(DocumentLine InvoiceLine, decimal? Quantity, decimal? Amount);
}
