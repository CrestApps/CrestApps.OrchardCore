using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Sends an estimate to its customer by e-mail (the <c>estimate-sent</c> kind), with its PDF and a link to the page where
/// they accept or decline it. A draft is sent (numbered) first.
/// </summary>
public sealed class EstimateSender
{
    private readonly IEstimateStore _estimates;
    private readonly IEstimateCommandService _commands;
    private readonly IInvoicePdfService _pdf;
    private readonly EstimateShareLinkService _links;
    private readonly ICommerceEmailSender _email;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public EstimateSender(
        IEstimateStore estimates,
        IEstimateCommandService commands,
        IInvoicePdfService pdf,
        EstimateShareLinkService links,
        ICommerceEmailSender email,
        ISession session,
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        ILogger<EstimateSender> logger,
        IStringLocalizer<EstimateSender> stringLocalizer)
    {
        _estimates = estimates;
        _commands = commands;
        _pdf = pdf;
        _links = links;
        _email = email;
        _session = session;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets whether e-mail is set up.
    /// </summary>
    public bool CanSend
        => _email.CanSend;

    /// <summary>
    /// Sends the estimate to an address, the bill-to's e-mail when <paramref name="to"/> is empty.
    /// </summary>
    public async Task<EstimateCommandResult> SendAsync(string estimateId, string to, CancellationToken cancellationToken = default)
    {
        var result = new EstimateCommandResult();
        var estimate = string.IsNullOrEmpty(estimateId) ? null : await _estimates.FindByIdAsync(estimateId, cancellationToken);

        if (estimate is null)
        {
            return result.Fail(S["The estimate could not be found."]);
        }

        result.Estimate = estimate;

        if (!_email.CanSend)
        {
            return result.Fail(S["E-mail isn't set up, so the estimate can't be sent. Copy its link instead."]);
        }

        if (estimate.Status == EstimateStatus.Draft)
        {
            var sent = await _commands.SendAsync(estimate.ItemId, cancellationToken);

            if (!sent.Succeeded)
            {
                foreach (var error in sent.Errors)
                {
                    result.Errors.Add(error);
                }

                return result;
            }

            estimate = sent.Estimate;
            result.Estimate = estimate;
        }
        else if (estimate.Status != EstimateStatus.Sent)
        {
            return result.Fail(S["Only an estimate waiting for an answer can be sent."]);
        }

        var address = string.IsNullOrWhiteSpace(to) ? estimate.BillTo?.Contact?.Email : to.Trim();

        if (string.IsNullOrEmpty(address))
        {
            return result.Fail(S["The estimate has no e-mail to send it to. Type one."]);
        }

        var pdf = await _pdf.GetAsync(estimate, cancellationToken);
        var values = await GetValuesAsync(estimate);

        var mail = await _email.SendWithResultAsync(new CommerceEmail
        {
            Kind = InvoicingConstants.EmailKinds.EstimateSent,
            To = address,
            Cc = [.. estimate.BillTo?.CopyTo ?? []],
            Models = new Dictionary<string, object>(StringComparer.Ordinal) { [InvoicingConstants.EmailKinds.DocumentModelName] = estimate },
            Culture = estimate.Culture,
            Reference = estimate.Number,
        }, _ => Task.FromResult(BuildContent(values, pdf)), cancellationToken);

        if (!mail.Succeeded)
        {
            return result.Fail(string.IsNullOrEmpty(mail.Error) ? S["The estimate could not be sent."].Value : S["The estimate could not be sent: {0}", mail.Error].Value);
        }

        var user = _httpContextAccessor.HttpContext?.User;

        estimate.History.Add(new DocumentEvent
        {
            Id = IdGenerator.GenerateId(),
            Kind = DocumentEventKinds.Sent,
            CreatedUtc = _clock.UtcNow,
            ActorId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorName = user?.Identity?.Name,
            Message = S["E-mailed to {0}.", address].Value,
        });

        try
        {
            await _estimates.UpdateAsync(estimate, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            _logger.LogWarning("Estimate '{EstimateId}' was e-mailed but changed meanwhile, so the delivery was not recorded on it.", estimate.ItemId);
        }

        return result;
    }

    private async Task<Dictionary<string, string>> GetValuesAsync(Estimate estimate)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [InvoicingConstants.EmailKinds.NumberValueName] = estimate.Number,
            [InvoicingConstants.EmailKinds.TotalValueName] = CurrencyFormat.WithCode(estimate.Totals?.Total ?? 0m, estimate.Currency),
            [InvoicingConstants.EmailKinds.CustomerNameValueName] = estimate.BillTo?.Contact?.Name,
            [InvoicingConstants.EmailKinds.StoreNameValueName] = estimate.Issuer?.Name,
            [InvoicingConstants.EmailKinds.ViewUrlValueName] = await _links.CreateUrlAsync(estimate),
        };

        if (estimate.ExpiryDate is { } expiry)
        {
            values[InvoicingConstants.EmailKinds.DueDateValueName] = expiry.ToString("d", CultureInfo.CurrentCulture);
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
        values.TryGetValue(InvoicingConstants.EmailKinds.DueDateValueName, out var expiry);
        values.TryGetValue(InvoicingConstants.EmailKinds.ViewUrlValueName, out var viewUrl);

        var lines = new List<string>
        {
            string.IsNullOrEmpty(name) ? S["Hello,"].Value : S["Hello {0},", name].Value,
            string.Empty,
            string.IsNullOrEmpty(expiry)
                ? S["Estimate {0} for {1} is attached.", number, total].Value
                : S["Estimate {0} for {1} is attached. It is valid until {2}.", number, total, expiry].Value,
        };

        var text = new StringBuilder(string.Join('\n', lines)).Append('\n');
        var html = new StringBuilder();

        foreach (var line in lines.Where(line => line.Length > 0))
        {
            html.Append("<p>").Append(WebUtility.HtmlEncode(line)).Append("</p>");
        }

        if (!string.IsNullOrEmpty(viewUrl))
        {
            text.Append('\n').Append(S["Accept or decline it online: {0}", viewUrl].Value).Append('\n');
            html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(viewUrl)).Append("\">").Append(WebUtility.HtmlEncode(S["Accept or decline it online"].Value)).Append("</a></p>");
        }

        if (!string.IsNullOrEmpty(store))
        {
            text.Append('\n').Append(store).Append('\n');
            html.Append("<p>").Append(WebUtility.HtmlEncode(store)).Append("</p>");
        }

        return new CommerceEmailContent
        {
            Subject = string.IsNullOrEmpty(store) ? S["Estimate {0}", number].Value : S["Estimate {0} from {1}", number, store].Value,
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
}
