using System.Text;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Reviews.Models;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.BackgroundTasks;
using OrchardCore.Email;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Reviews.Services;

/// <summary>
/// Asks buyers to review what they bought: one email for each paid order, a number of days after it was paid, with a
/// link to each product's reviews. Only buyers who accept marketing are asked, and only within 60 days of paying.
/// </summary>
public sealed class ReviewRequestService
{
    public const int BatchSize = 200;

    // Orders paid longer ago than this aren't asked about.
    private const int WindowDays = 60;

    private readonly IOrderStore _orders;
    private readonly ICustomerStore _customers;
    private readonly IEmailService _emailService;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public ReviewRequestService(
        IOrderStore orders,
        ICustomerStore customers,
        IEmailService emailService,
        ISiteService siteService,
        IClock clock,
        ILogger<ReviewRequestService> logger,
        IStringLocalizer<ReviewRequestService> stringLocalizer,
        LinkGenerator linkGenerator)
    {
        _orders = orders;
        _customers = customers;
        _emailService = emailService;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Emails the review requests that are due. Returns how many went.
    /// </summary>
    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<ReviewSettings>();
        var site = await _siteService.GetSiteSettingsAsync();

        if (!settings.SendReviewRequests || string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var paidBefore = now.AddDays(-Math.Clamp(settings.ReviewRequestDelayDays, 0, WindowDays));
        var due = new List<Order>();

        // Orders placed in the window, read a page at a time until a batch is found; those already asked are skipped.
        for (var skip = 0; due.Count < BatchSize; skip += 500)
        {
            var (_, page) = await _orders.PageAsync(new OrderQuery
            {
                Statuses = [OrderStatus.Paid],
                CreatedOnOrAfter = now.AddDays(-WindowDays),
                CreatedBefore = paidBefore,
            }, skip, 500, cancellationToken);

            due.AddRange(page.Where(order => order.ReviewRequestSentUtc is null && (order.PaidUtc ?? order.CreatedUtc) <= paidBefore));

            if (page.Count < 500)
            {
                break;
            }
        }

        var sent = 0;

        foreach (var order in due.Take(BatchSize))
        {
            // Asked once either way: a buyer who doesn't accept marketing isn't asked later.
            order.ReviewRequestSentUtc = now;

            var customer = string.IsNullOrEmpty(order.CustomerId) ? null : await _customers.FindByIdAsync(order.CustomerId, cancellationToken);

            if (customer is { AcceptsMarketing: true } && !string.IsNullOrWhiteSpace(order.CustomerEmail) && order.Lines.Any(line => !string.IsNullOrEmpty(line.ContentItemId)))
            {
                using var language = OrderCulture.Use(order.Culture);

                var (subject, text, html) = Build(order, site.BaseUrl, site.SiteName);

                try
                {
                    var result = await _emailService.SendAsync(new MailMessage { To = order.CustomerEmail, Subject = subject, TextBody = text, HtmlBody = html }, cancellationToken: cancellationToken);

                    if (result.Succeeded)
                    {
                        sent++;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogError(exception, "The review request for order {OrderNumber} couldn't be sent.", order.OrderNumber);
                }
            }

            await _orders.UpdateAsync(order, cancellationToken);
        }

        return sent;
    }

    internal (string Subject, string Text, string Html) Build(Order order, string baseUrl, string storeName)
    {
        var encoder = HtmlEncoder.Default;
        var subject = S["How was your order from {0}?", storeName].Value;
        var lead = string.IsNullOrWhiteSpace(order.CustomerName)
            ? S["Thanks for your order {0}. Would you tell other shoppers what you think of it?", order.OrderNumber].Value
            : S["Hi {0}, thanks for your order {1}. Would you tell other shoppers what you think of it?", order.CustomerName, order.OrderNumber].Value;

        var text = new StringBuilder().AppendLine(lead).AppendLine();
        var html = new StringBuilder("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529;max-width:600px\">")
            .Append("<p>").Append(encoder.Encode(lead)).Append("</p><ul>");

        foreach (var line in order.Lines.Where(line => !string.IsNullOrEmpty(line.ContentItemId)).DistinctBy(line => line.ContentItemId))
        {
            // The site's address holds the tenant's prefix, so the route's path goes after it as is.
            var path = _linkGenerator.GetPathByAction("Display", "Item", new { area = "OrchardCore.Contents", contentItemId = line.ContentItemId });
            var url = $"{baseUrl.TrimEnd('/')}{path}#reviews";
            var review = S["Review {0}", line.Title].Value;

            text.AppendLine($"{review}: {url}");
            html.Append("<li style=\"margin:6px 0\"><a href=\"").Append(encoder.Encode(url)).Append("\">").Append(encoder.Encode(review)).Append("</a></li>");
        }

        var why = S["You're getting this because you said yes to news from us. Change that in your account."].Value;

        text.AppendLine().AppendLine(why);
        html.Append("</ul><p style=\"font-size:12px;color:#6c757d\">").Append(encoder.Encode(why)).Append("</p></div>");

        return (subject, text.ToString(), html.ToString());
    }
}

/// <summary>
/// Once an hour, emails the review requests that are due.
/// </summary>
[BackgroundTask(
    Title = "Review requests",
    Schedule = "15 * * * *",
    Description = "Asks buyers who accept marketing to review what they bought, some days after paying.",
    LockTimeout = 3_000,
    LockExpiration = 900_000,
    UsePipeline = true)]
public sealed class ReviewRequestBackgroundTask : IBackgroundTask
{
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => serviceProvider.GetRequiredService<ReviewRequestService>().SendDueAsync(cancellationToken);
}
