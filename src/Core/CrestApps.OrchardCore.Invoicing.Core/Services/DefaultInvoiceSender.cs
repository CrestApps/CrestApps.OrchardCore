using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Sends an invoice as the <see cref="InvoicingConstants.EmailKinds.InvoiceIssued"/> commerce e-mail, with its PDF
/// attached, its share link and its pay link, and records the delivery on the invoice. A site changes the message with
/// the e-mail kind's template.
/// </summary>
public sealed class DefaultInvoiceSender : IInvoiceSender
{
    private readonly IInvoiceStore _invoices;
    private readonly IInvoiceCommandService _commands;
    private readonly IInvoicePdfService _pdf;
    private readonly IInvoiceShareLinkService _shareLinks;
    private readonly ICommerceEmailSender _email;
    private readonly ISiteService _siteService;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultInvoiceSender(
        IInvoiceStore invoices,
        IInvoiceCommandService commands,
        IInvoicePdfService pdf,
        IInvoiceShareLinkService shareLinks,
        ICommerceEmailSender email,
        ISiteService siteService,
        ISession session,
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        ILogger<DefaultInvoiceSender> logger,
        IStringLocalizer<DefaultInvoiceSender> stringLocalizer)
    {
        _invoices = invoices;
        _commands = commands;
        _pdf = pdf;
        _shareLinks = shareLinks;
        _email = email;
        _siteService = siteService;
        _session = session;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<InvoiceSendResult> SendAsync(InvoiceSendRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new InvoiceSendResult();
        var invoice = string.IsNullOrEmpty(request.InvoiceId) ? null : await _invoices.FindByIdAsync(request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            result.Errors.Add(S["The invoice could not be found."]);

            return result;
        }

        result.Invoice = invoice;

        if (!_email.CanSend)
        {
            result.Errors.Add(S["E-mail isn't set up, so the invoice can't be sent. Copy its share link instead."]);

            return result;
        }

        if (invoice.Status == InvoiceStatus.Void)
        {
            result.Errors.Add(S["A void invoice can't be sent."]);

            return result;
        }

        if (invoice.Status == InvoiceStatus.Draft)
        {
            var site = await _siteService.GetSiteSettingsAsync();

            if (site.GetOrCreate<InvoicingSettings>()?.IssueOnSend != true)
            {
                result.Errors.Add(S["Issue the invoice before sending it."]);

                return result;
            }

            var issued = await _commands.IssueAsync(invoice.ItemId, cancellationToken);

            if (!issued.Succeeded)
            {
                foreach (var error in issued.Errors)
                {
                    result.Errors.Add(error);
                }

                return result;
            }

            invoice = issued.Invoice;
            result.Invoice = invoice;
            result.Issued = true;
        }

        var to = Clean(request.To) ?? invoice.BillTo?.Contact?.Email;

        if (string.IsNullOrEmpty(to))
        {
            result.Errors.Add(S["The invoice has no e-mail to send it to. Type one."]);

            return result;
        }

        var cc = Addresses(request.Cc).Count > 0 ? Addresses(request.Cc) : Addresses(invoice.BillTo?.CopyTo);
        var bcc = Addresses(request.Bcc);
        var pdf = await _pdf.GetAsync(invoice, cancellationToken);
        var values = await GetValuesAsync(invoice, cancellationToken);

        var sent = await _email.SendWithResultAsync(new CommerceEmail
        {
            Kind = InvoicingConstants.EmailKinds.InvoiceIssued,
            To = to,
            Cc = cc,
            Bcc = bcc,
            Models = new Dictionary<string, object>(StringComparer.Ordinal) { [InvoicingConstants.EmailKinds.DocumentModelName] = invoice },
            Culture = invoice.Culture,
            Reference = invoice.Number,
        }, _ => Task.FromResult(BuildContent(values, pdf)), cancellationToken);

        if (!sent.Succeeded)
        {
            result.Errors.Add(string.IsNullOrEmpty(sent.Error)
                ? S["The invoice could not be sent."].Value
                : S["The invoice could not be sent: {0}", sent.Error].Value);

            return result;
        }

        result.Invoice = await RecordAsync(invoice.ItemId, to, cc, cancellationToken) ?? invoice;

        return result;
    }

    // The delivery is recorded on the invoice read again, since issuing it may have committed the session.
    private async Task<Invoice> RecordAsync(string invoiceId, string to, List<string> cc, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.FindByIdAsync(invoiceId, cancellationToken);

        if (invoice is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var user = _httpContextAccessor.HttpContext?.User;
        var recipients = cc.Count == 0 ? to : $"{to}, {string.Join(", ", cc)}";

        invoice.SentUtc ??= now;
        invoice.Delivery = InvoiceLifecycle.AdvanceDelivery(invoice.Delivery, InvoiceDelivery.Sent);
        invoice.ModifiedUtc = now;
        invoice.History.Add(new DocumentEvent
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentEventKinds.Sent,
            CreatedUtc = now,
            ActorId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = user?.Identity?.Name,
            Message = S["Sent to {0}.", recipients].Value,
        });

        try
        {
            await _invoices.UpdateAsync(invoice, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _logger.LogWarning("Invoice '{InvoiceId}' was sent but changed meanwhile, so the delivery was not recorded on it.", invoiceId);
        }

        return invoice;
    }

    private async Task<Dictionary<string, string>> GetValuesAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var currency = invoice.Currency;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [InvoicingConstants.EmailKinds.NumberValueName] = invoice.Number,
            [InvoicingConstants.EmailKinds.TotalValueName] = CurrencyFormat.WithCode(invoice.Totals?.Total ?? 0m, currency),
            [InvoicingConstants.EmailKinds.AmountDueValueName] = CurrencyFormat.WithCode(invoice.AmountDue, currency),
            [InvoicingConstants.EmailKinds.CustomerNameValueName] = invoice.BillTo?.Contact?.Name,
            [InvoicingConstants.EmailKinds.StoreNameValueName] = invoice.Issuer?.Name,
            [InvoicingConstants.EmailKinds.ViewUrlValueName] = await _shareLinks.CreateUrlAsync(invoice, cancellationToken),
        };

        if (invoice.DueDate is { } dueDate)
        {
            values[InvoicingConstants.EmailKinds.DueDateValueName] = dueDate.ToString("d", CultureInfo.CurrentCulture);
        }

        if (invoice.AmountDue > 0m && !string.IsNullOrEmpty(invoice.TransactionId) &&
            _serviceProvider.GetService<ITransactionPayLinkService>() is { } payLinks &&
            await _serviceProvider.GetRequiredService<ITransactionManager>().FindByIdAsync(invoice.TransactionId, cancellationToken) is { } transaction)
        {
            values[InvoicingConstants.EmailKinds.PayUrlValueName] = await payLinks.CreatePayUrlAsync(transaction, cancellationToken);
        }

        return values;
    }

    // The message the e-mail kind sends when the site has not changed its template.
    private CommerceEmailContent BuildContent(Dictionary<string, string> values, InvoicePdf pdf)
    {
        values.TryGetValue(InvoicingConstants.EmailKinds.NumberValueName, out var number);
        values.TryGetValue(InvoicingConstants.EmailKinds.StoreNameValueName, out var store);
        values.TryGetValue(InvoicingConstants.EmailKinds.CustomerNameValueName, out var name);
        values.TryGetValue(InvoicingConstants.EmailKinds.TotalValueName, out var total);
        values.TryGetValue(InvoicingConstants.EmailKinds.AmountDueValueName, out var due);
        values.TryGetValue(InvoicingConstants.EmailKinds.DueDateValueName, out var dueDate);
        values.TryGetValue(InvoicingConstants.EmailKinds.ViewUrlValueName, out var viewUrl);
        values.TryGetValue(InvoicingConstants.EmailKinds.PayUrlValueName, out var payUrl);

        var lines = new List<string>
        {
            string.IsNullOrEmpty(name) ? S["Hello,"].Value : S["Hello {0},", name].Value,
            string.Empty,
            string.IsNullOrEmpty(dueDate)
                ? S["Invoice {0} for {1} is attached.", number, total].Value
                : S["Invoice {0} for {1} is attached. It is due on {2}.", number, total, dueDate].Value,
        };

        if (!string.IsNullOrEmpty(due) && !string.Equals(due, total, StringComparison.Ordinal))
        {
            lines.Add(S["Still due: {0}.", due].Value);
        }

        var text = new StringBuilder(string.Join('\n', lines)).Append('\n');
        var html = new StringBuilder();

        foreach (var line in lines.Where(line => line.Length > 0))
        {
            html.Append("<p>").Append(WebUtility.HtmlEncode(line)).Append("</p>");
        }

        if (!string.IsNullOrEmpty(payUrl))
        {
            text.Append('\n').Append(S["Pay it online: {0}", payUrl].Value).Append('\n');
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(payUrl)).Append("\">").Append(WebUtility.HtmlEncode(S["Pay it online"].Value)).Append("</a></p>");
        }

        if (!string.IsNullOrEmpty(viewUrl))
        {
            text.Append(S["View it online: {0}", viewUrl].Value).Append('\n');
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(viewUrl)).Append("\">").Append(WebUtility.HtmlEncode(S["View it online"].Value)).Append("</a></p>");
        }

        if (!string.IsNullOrEmpty(store))
        {
            text.Append('\n').Append(store).Append('\n');
            html.Append("<p>").Append(WebUtility.HtmlEncode(store)).Append("</p>");
        }

        return new CommerceEmailContent
        {
            Subject = string.IsNullOrEmpty(store)
                ? S["Invoice {0}", number].Value
                : S["Invoice {0} from {1}", number, store].Value,
            Text = text.ToString(),
            Html = html.ToString(),
            Values = values,
            Attachments =
            [
                new CommerceEmailAttachment
                {
                    FileName = pdf.FileName,
                    ContentType = "application/pdf",
                    OpenAsync = _ => Task.FromResult<Stream>(new MemoryStream(pdf.Content)),
                },
            ],
        };
    }

    private static List<string> Addresses(IEnumerable<string> addresses)
        => [.. (addresses ?? []).Select(Clean).Where(address => address is not null).Distinct(StringComparer.OrdinalIgnoreCase)];

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
