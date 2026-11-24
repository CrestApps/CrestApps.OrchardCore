using System.Globalization;
using System.Net;
using System.Text;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The default <see cref="IInvoiceStatementService"/>. The balance moves with each invoice issued (up), each payment,
/// adjustment and credit note that lowers what is owed (down), and each payment taken back or refund (up), so an
/// activity statement's closing balance is what the customer owes, or the credit they hold when it is negative.
/// </summary>
public sealed class DefaultInvoiceStatementService : IInvoiceStatementService
{
    private const int MaxInvoices = 1000;

    private const string _date = "date";
    private const string _document = "document";
    private const string _description = "description";
    private const string _charge = "charge";
    private const string _credit = "credit";
    private const string _balance = "balance";

    private readonly IInvoiceManager _invoices;
    private readonly ICreditNoteStore _creditNotes;
    private readonly ITransactionManager _transactions;
    private readonly ILocalClock _localClock;
    private readonly IMoneyFormatter _moneyFormatter;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    public DefaultInvoiceStatementService(
        IInvoiceManager invoices,
        ICreditNoteStore creditNotes,
        ITransactionManager transactions,
        ILocalClock localClock,
        IMoneyFormatter moneyFormatter,
        IServiceProvider serviceProvider,
        IStringLocalizer<DefaultInvoiceStatementService> stringLocalizer)
    {
        _invoices = invoices;
        _creditNotes = creditNotes;
        _transactions = transactions;
        _localClock = localClock;
        _moneyFormatter = moneyFormatter;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<InvoiceStatement> BuildAsync(InvoiceStatementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var statement = new InvoiceStatement { Request = request };

        if (request.Party.IsEmpty)
        {
            return statement;
        }

        var billed = (await _invoices.PageAsync(1, MaxInvoices, new InvoiceQuery { BillTo = [request.Party], OnlyBillTo = true, IssuedOnly = true }, cancellationToken)).Entries
            .Where(invoice => invoice.Status is not (InvoiceStatus.Draft or InvoiceStatus.Void) && invoice.IssueDate is not null)
            .OrderBy(invoice => invoice.IssueDate)
            .ThenBy(invoice => invoice.Number, StringComparer.Ordinal)
            .ToList();

        var latest = billed.LastOrDefault();

        statement.Currency = !string.IsNullOrEmpty(request.Currency) ? request.Currency.ToUpperInvariant() : latest?.Currency;
        statement.Recipient = latest?.BillTo;
        statement.Issuer = latest?.Issuer;

        foreach (var currency in billed.Select(invoice => invoice.Currency).Where(currency => !string.IsNullOrEmpty(currency)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!string.Equals(currency, statement.Currency, StringComparison.OrdinalIgnoreCase))
            {
                statement.OtherCurrencies.Add(currency);
            }
        }

        var invoices = billed.Where(invoice => string.Equals(invoice.Currency, statement.Currency, StringComparison.OrdinalIgnoreCase)).ToList();

        if (request.Kind == InvoiceStatementKind.OpenItem)
        {
            BuildOpenItems(statement, invoices, request.To);

            return statement;
        }

        var entries = new List<InvoiceStatementLine>();

        foreach (var invoice in invoices)
        {
            await CollectAsync(invoice, entries, cancellationToken);
        }

        var from = request.From ?? DateOnly.MinValue;
        var balance = entries.Where(entry => entry.Date < from).Sum(entry => entry.Charge - entry.Credit);

        statement.OpeningBalance = balance;

        foreach (var entry in entries.Where(entry => entry.Date >= from && entry.Date <= request.To).OrderBy(entry => entry.Date).ThenBy(entry => entry.Kind))
        {
            balance += entry.Charge - entry.Credit;
            entry.Balance = balance;
            statement.Lines.Add(entry);
        }

        statement.ClosingBalance = balance;

        return statement;
    }

    /// <inheritdoc/>
    public async Task<BusinessDocument> BuildDocumentAsync(InvoiceStatement statement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        var culture = CultureInfo.CurrentCulture;
        var request = statement.Request ?? new InvoiceStatementRequest();

        string Money(decimal amount)
            => string.IsNullOrEmpty(statement.Currency) ? amount.ToString("0.00", culture) : _moneyFormatter.Format(amount, statement.Currency);

        var document = new BusinessDocument
        {
            Kind = InvoicingConstants.BusinessDocumentKinds.Statement,
            Title = request.Kind == InvoiceStatementKind.OpenItem ? S["Statement of open invoices"] : S["Statement of account"],
            Issuer = await GetPartyAsync(statement.Issuer?.Name, statement.Issuer?.Address, statement.Issuer?.TaxId, statement.Issuer?.Email),
            LogoUrl = statement.Issuer?.LogoUrl,
            Recipient = await GetPartyAsync(statement.Recipient?.Contact?.Name, statement.Recipient?.Contact?.Address, statement.Recipient?.TaxId, statement.Recipient?.Contact?.Email),
            RecipientTitle = S["Customer"],
        };

        // The statement's days are days in the site's time zone, kept as dates: printed as they are.
        if (request.Kind == InvoiceStatementKind.Activity)
        {
            document.Fields.Add(new BusinessDocumentField(S["Period"], request.From is { } from
                ? S["{0} to {1}", from.ToString("d", culture), request.To.ToString("d", culture)]
                : S["Up to {0}", request.To.ToString("d", culture)]));
        }
        else
        {
            document.Fields.Add(new BusinessDocumentField(S["Date"], request.To.ToString("d", culture)));
        }

        document.Fields.Add(new BusinessDocumentField(S["Currency"], statement.Currency));

        if (request.Kind == InvoiceStatementKind.Activity)
        {
            document.Columns =
            [
                new BusinessDocumentColumn(_date, S["Date"], BusinessDocumentAlignment.Start, 14),
                new BusinessDocumentColumn(_document, S["Document"], BusinessDocumentAlignment.Start, 16),
                new BusinessDocumentColumn(_description, S["Description"]),
                new BusinessDocumentColumn(_charge, S["Charges"], BusinessDocumentAlignment.End, 14),
                new BusinessDocumentColumn(_credit, S["Credits"], BusinessDocumentAlignment.End, 14),
                new BusinessDocumentColumn(_balance, S["Balance"], BusinessDocumentAlignment.End, 14),
            ];

            document.Rows.Add(new BusinessDocumentRow
            {
                Kind = BusinessDocumentRowKind.Item,
                Cells =
                {
                    [_date] = (request.From ?? request.To).ToString("d", culture),
                    [_description] = S["Balance brought forward"],
                    [_balance] = Money(statement.OpeningBalance),
                },
            });

            foreach (var line in statement.Lines)
            {
                document.Rows.Add(new BusinessDocumentRow
                {
                    Kind = BusinessDocumentRowKind.Item,
                    Cells =
                    {
                        [_date] = line.Date.ToString("d", culture),
                        [_document] = line.DocumentNumber,
                        [_description] = line.Description,
                        [_charge] = line.Charge > 0m ? Money(line.Charge) : string.Empty,
                        [_credit] = line.Credit > 0m ? Money(line.Credit) : string.Empty,
                        [_balance] = Money(line.Balance),
                    },
                });
            }

            document.Totals.Add(new BusinessDocumentTotal(S["Opening balance"], Money(statement.OpeningBalance)));
            document.Totals.Add(new BusinessDocumentTotal(S["Charges"], Money(statement.Lines.Sum(line => line.Charge))));
            document.Totals.Add(new BusinessDocumentTotal(S["Credits"], Money(statement.Lines.Sum(line => line.Credit))));
            document.Totals.Add(new BusinessDocumentTotal(statement.ClosingBalance < 0m ? S["Credit held"] : S["Balance due"], Money(Math.Abs(statement.ClosingBalance)), emphasized: true));
        }
        else
        {
            document.Columns =
            [
                new BusinessDocumentColumn(_document, S["Invoice"], BusinessDocumentAlignment.Start, 16),
                new BusinessDocumentColumn(_date, S["Date"], BusinessDocumentAlignment.Start, 14),
                new BusinessDocumentColumn(_description, S["Due"], BusinessDocumentAlignment.Start, 14),
                new BusinessDocumentColumn(_charge, S["Total"], BusinessDocumentAlignment.End, 14),
                new BusinessDocumentColumn(_credit, S["Paid and credited"], BusinessDocumentAlignment.End, 16),
                new BusinessDocumentColumn(_balance, S["Still due"], BusinessDocumentAlignment.End, 14),
            ];

            foreach (var item in statement.OpenItems)
            {
                var invoice = item.Invoice;
                var total = invoice.Totals?.Total ?? 0m;

                document.Rows.Add(new BusinessDocumentRow
                {
                    Kind = BusinessDocumentRowKind.Item,
                    Cells =
                    {
                        [_document] = invoice.Number,
                        [_date] = invoice.IssueDate?.ToString("d", culture),
                        [_description] = item.DaysOverdue > 0
                            ? S["{0} ({1} days late)", invoice.DueDate?.ToString("d", culture), item.DaysOverdue]
                            : invoice.DueDate?.ToString("d", culture),
                        [_charge] = Money(total),
                        [_credit] = Money(total - invoice.AmountDue),
                        [_balance] = Money(invoice.AmountDue),
                    },
                });
            }

            document.Totals.Add(new BusinessDocumentTotal(S["Not due yet"], Money(statement.Aging[0]), muted: statement.Aging[0] == 0m));
            document.Totals.Add(new BusinessDocumentTotal(S["1 to 30 days late"], Money(statement.Aging[1]), muted: statement.Aging[1] == 0m));
            document.Totals.Add(new BusinessDocumentTotal(S["31 to 60 days late"], Money(statement.Aging[2]), muted: statement.Aging[2] == 0m));
            document.Totals.Add(new BusinessDocumentTotal(S["61 to 90 days late"], Money(statement.Aging[3]), muted: statement.Aging[3] == 0m));
            document.Totals.Add(new BusinessDocumentTotal(S["Over 90 days late"], Money(statement.Aging[4]), muted: statement.Aging[4] == 0m));
            document.Totals.Add(new BusinessDocumentTotal(S["Balance due"], Money(statement.ClosingBalance), emphasized: true));
        }

        if (statement.OtherCurrencies.Count > 0)
        {
            document.Sections.Add(new BusinessDocumentSection(S["Other currencies"], S["This statement is in {0}. Invoices in {1} are on a statement of their own.", statement.Currency, string.Join(", ", statement.OtherCurrencies)]));
        }

        if (_serviceProvider.GetService<IBusinessDocumentTemplateManager>() is { } templates)
        {
            await templates.ApplyAsync(document, templateId: null, cancellationToken);
        }

        return document;
    }

    /// <inheritdoc/>
    public async Task<string> SendAsync(InvoiceStatement statement, string to = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        to = string.IsNullOrWhiteSpace(to) ? statement.Recipient?.Contact?.Email : to.Trim();

        if (string.IsNullOrEmpty(to))
        {
            return S["The customer has no e-mail address to send the statement to."];
        }

        if (_serviceProvider.GetService<ICommerceEmailSender>() is not { CanSend: true } email)
        {
            return S["E-mail isn't set up, so the statement can't be sent. Download its PDF instead."];
        }

        var document = await BuildDocumentAsync(statement, cancellationToken);
        var pdf = _serviceProvider.GetService<IBusinessDocumentPdfRenderer>() is { } renderer ? await renderer.RenderAsync(document, cancellationToken) : null;
        var name = statement.Recipient?.Contact?.Name;
        var store = statement.Issuer?.Name;
        var due = string.IsNullOrEmpty(statement.Currency) ? statement.ClosingBalance.ToString("0.00", CultureInfo.CurrentCulture) : _moneyFormatter.Format(statement.ClosingBalance, statement.Currency);

        var lines = new[]
        {
            string.IsNullOrEmpty(name) ? S["Hello,"].Value : S["Hello {0},", name].Value,
            S["Your statement is attached. The balance due is {0}.", due].Value,
            store ?? string.Empty,
        };

        var html = new StringBuilder();

        foreach (var line in lines.Where(line => line.Length > 0))
        {
            html.Append("<p>").Append(WebUtility.HtmlEncode(line)).Append("</p>");
        }

        var sent = await email.SendWithResultAsync(new CommerceEmail
        {
            Kind = InvoicingConstants.EmailKinds.Statement,
            To = to,
            Reference = document.Title,
        }, _ => Task.FromResult(new CommerceEmailContent
        {
            Subject = string.IsNullOrEmpty(store) ? document.Title : S["{0} from {1}", document.Title, store].Value,
            Text = string.Join('\n', lines),
            Html = html.ToString(),
            Values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CustomerName"] = name,
                ["BalanceDue"] = due,
                ["StoreName"] = store,
            },
            Attachments = pdf is null ? [] :
            [
                new CommerceEmailAttachment
                {
                    FileName = "statement.pdf",
                    ContentType = "application/pdf",
                    OpenAsync = _ => Task.FromResult<Stream>(new MemoryStream(pdf)),
                },
            ],
        }), cancellationToken);

        return sent.Succeeded ? null : sent.Error ?? S["The statement could not be sent."].Value;
    }

    // The invoices still owed on the day, oldest due first, and what is owed by how late it is.
    private static void BuildOpenItems(InvoiceStatement statement, List<Invoice> invoices, DateOnly day)
    {
        foreach (var invoice in invoices.Where(invoice => invoice.Status == InvoiceStatus.Open && invoice.AmountDue > 0m).OrderBy(invoice => invoice.DueDate ?? invoice.IssueDate))
        {
            var late = invoice.DueDate is { } due ? Math.Max(0, day.DayNumber - due.DayNumber) : 0;
            var bucket = late switch
            {
                0 => 0,
                <= 30 => 1,
                <= 60 => 2,
                <= 90 => 3,
                _ => 4,
            };

            statement.Aging[bucket] += invoice.AmountDue;
            statement.OpenItems.Add(new InvoiceStatementOpenItem { Invoice = invoice, DaysOverdue = late });
        }

        statement.ClosingBalance = statement.OpenItems.Sum(item => item.Invoice.AmountDue);
    }

    // The entries one invoice adds: its issue, then what the ledger recorded against it, and the credit notes that
    // refunded or kept the amount as credit (a credit note that lowers the balance is the ledger's adjustment already).
    private async Task CollectAsync(Invoice invoice, List<InvoiceStatementLine> entries, CancellationToken cancellationToken)
    {
        var kind = invoice.Kind switch
        {
            InvoiceKind.DebitNote => S["Debit note"].Value,
            InvoiceKind.Prepayment => S["Deposit invoice"].Value,
            _ => S["Invoice"].Value,
        };

        entries.Add(new InvoiceStatementLine
        {
            Date = invoice.IssueDate.Value,
            Kind = InvoiceStatementLineKind.Invoice,
            DocumentNumber = invoice.Number,
            InvoiceId = invoice.ItemId,
            Description = string.IsNullOrEmpty(invoice.Reference) ? kind : S["{0}: {1}", kind, invoice.Reference].Value,
            Charge = invoice.Totals?.Total ?? 0m,
        });

        if (!string.IsNullOrEmpty(invoice.TransactionId) && await _transactions.FindByIdAsync(invoice.TransactionId, cancellationToken) is { } transaction)
        {
            // What is still owed on the invoice as its entries go by, so a debt canceled on the ledger credits that.
            var owed = invoice.Totals?.Total ?? 0m;

            foreach (var entry in transaction.Events.OrderBy(entry => entry.CreatedUtc))
            {
                InvoiceStatementLineKind lineKind;
                string description;
                bool charge;
                var amount = entry.Amount ?? 0m;

                switch (entry.Type)
                {
                    case TransactionEventType.Canceled when owed > 0m:
                        (lineKind, description, charge, amount) = (InvoiceStatementLineKind.Adjustment, string.IsNullOrEmpty(entry.Reason) ? S["Canceled"].Value : S["Canceled: {0}", entry.Reason].Value, false, owed);
                        break;
                    case TransactionEventType.Canceled:
                        continue;
                    case TransactionEventType.PaymentRecorded:
                        (lineKind, description, charge) = (InvoiceStatementLineKind.Payment, S["Payment, thank you"].Value, false);
                        break;
                    case TransactionEventType.Adjusted:
                        lineKind = entry.AdjustmentKind == TransactionAdjustmentKind.Credit ? InvoiceStatementLineKind.CreditNote : InvoiceStatementLineKind.Adjustment;
                        description = string.IsNullOrEmpty(entry.Reason) ? S["Adjustment"].Value : entry.Reason;
                        charge = false;
                        break;
                    case TransactionEventType.Reversed:
                        (lineKind, description, charge) = (InvoiceStatementLineKind.Reversal, S["Payment or adjustment taken back"].Value, true);
                        break;
                    case TransactionEventType.Refunded:
                        (lineKind, description, charge) = (InvoiceStatementLineKind.Refund, S["Refund"].Value, true);
                        break;
                    default:
                        continue;
                }

                if (amount <= 0m)
                {
                    continue;
                }

                owed += charge ? amount : -amount;

                entries.Add(new InvoiceStatementLine
                {
                    Date = DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(entry.CreatedUtc, DateTimeKind.Utc))).DateTime),
                    Kind = lineKind,
                    DocumentNumber = invoice.Number,
                    InvoiceId = invoice.ItemId,
                    Description = description,
                    Charge = charge ? amount : 0m,
                    Credit = charge ? 0m : amount,
                });
            }
        }

        foreach (var creditNote in await _creditNotes.ListByInvoiceAsync(invoice.ItemId, cancellationToken))
        {
            if (creditNote.Status != CreditNoteStatus.Issued || creditNote.IssuedUtc is not { } issuedUtc || creditNote.Disposition == CreditNoteDisposition.ReduceBalance)
            {
                continue;
            }

            entries.Add(new InvoiceStatementLine
            {
                Date = creditNote.IssueDate ?? DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(issuedUtc, DateTimeKind.Utc))).DateTime),
                Kind = InvoiceStatementLineKind.CreditNote,
                DocumentNumber = creditNote.Number,
                InvoiceId = invoice.ItemId,
                Description = S["Credit note for invoice {0}", invoice.Number].Value,
                Credit = creditNote.Totals?.Total ?? 0m,
            });
        }
    }

    private async Task<BusinessDocumentParty> GetPartyAsync(string name, Address address, string taxId, string email)
    {
        if (string.IsNullOrEmpty(name) && address is null)
        {
            return null;
        }

        IReadOnlyList<string> lines = [];

        if (address is not null)
        {
            lines = _serviceProvider.GetService<IAddressFormatter>() is { } formatter
                ? await formatter.GetLinesAsync(address)
                : [.. new[] { address.AddressLine1, address.AddressLine2, address.City, address.Region, address.PostalCode, address.Country }.Where(part => !string.IsNullOrWhiteSpace(part))];
        }

        return new BusinessDocumentParty { Name = name, Lines = [.. lines], TaxId = taxId, Email = email };
    }
}
