using System.Net;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.Workflows;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Reports.Subscriptions.Services;

/// <summary>
/// Runs a subscription's report for the period before it, and emails its headline numbers and tables, with the report as
/// a file when the subscription asks for one.
/// </summary>
public sealed class ReportSubscriptionService
{
    private readonly IReportSubscriptionStore _store;
    private readonly IReportManager _reports;
    private readonly IReportExportManager _exports;
    private readonly ReportDisplayValueResolver _displayValues;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ISiteService _siteService;
    private readonly IEmailService _emailService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public ReportSubscriptionService(
        IReportSubscriptionStore store,
        IReportManager reports,
        IReportExportManager exports,
        ReportDisplayValueResolver displayValues,
        IStringLocalizerFactory localizerFactory,
        ISiteService siteService,
        IClock clock,
        ILogger<ReportSubscriptionService> logger,
        IStringLocalizer<ReportSubscriptionService> stringLocalizer,
        IEmailService emailService = null)
    {
        _store = store;
        _reports = reports;
        _exports = exports;
        _displayValues = displayValues;
        _localizerFactory = localizerFactory;
        _siteService = siteService;
        _emailService = emailService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public bool CanSend
        => _emailService is not null;

    /// <summary>
    /// Returns the site's time zone, which schedules and periods are in.
    /// </summary>
    public async Task<TimeZoneInfo> GetTimeZoneAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        return !string.IsNullOrEmpty(site?.TimeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(site.TimeZoneId, out var timeZone)
            ? timeZone
            : TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Sets when a subscription next goes out, from now.
    /// </summary>
    public async Task ScheduleAsync(ReportSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        subscription.NextRunUtc = subscription.IsEnabled
            ? ReportSubscriptionSchedule.GetNextRunUtc(subscription, _clock.UtcNow, await GetTimeZoneAsync())
            : null;
    }

    /// <summary>
    /// Emails a subscription's report for the period before <paramref name="runUtc"/>, and schedules the next one.
    /// Returns whether it went.
    /// </summary>
    public async Task<bool> SendAsync(ReportSubscription subscription, DateTime runUtc, bool reschedule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var timeZone = await GetTimeZoneAsync();
        var sent = false;

        try
        {
            subscription.LastError = await TrySendAsync(subscription, runUtc, timeZone, cancellationToken);
            sent = subscription.LastError is null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Report subscription '{SubscriptionId}' couldn't run its report.", subscription.ItemId);
            subscription.LastError = S["The report couldn't run."];
        }

        subscription.LastRunUtc = runUtc;

        if (reschedule)
        {
            subscription.NextRunUtc = ReportSubscriptionSchedule.GetNextRunUtc(subscription, runUtc, timeZone);
        }

        subscription.ModifiedUtc = _clock.UtcNow;
        await _store.UpdateAsync(subscription, cancellationToken);

        return sent;
    }

    private async Task<string> TrySendAsync(ReportSubscription subscription, DateTime runUtc, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        if (_emailService is null)
        {
            return S["The site's email isn't set up."];
        }

        if (string.IsNullOrWhiteSpace(subscription.Recipients))
        {
            return S["It has no recipients."];
        }

        if (_reports.GetDescriptor(subscription.ReportName) is not { } descriptor)
        {
            return S["The report {0} isn't available; its feature may be off.", subscription.ReportName];
        }

        var (fromUtc, toUtc) = ReportSubscriptionSchedule.GetPeriod(subscription, runUtc, timeZone);
        var filter = new ReportFilter { ReportName = descriptor.Name };

        filter.SetDateRange(new ReportDateRange { FromUtc = fromUtc, ToUtc = toUtc });

        var hasAttachment = !string.IsNullOrWhiteSpace(subscription.AttachmentFormat);
        var document = await _reports.GetReport(descriptor.Name).RunAsync(new ReportContext(filter, hasAttachment ? ReportRunMode.Export : ReportRunMode.Display), cancellationToken);
        var title = _localizerFactory.Localize(descriptor.DisplayName).Value;

        document.Title = title;
        await _displayValues.ResolveAsync(document);

        var fromLocal = TimeZoneInfo.ConvertTimeFromUtc(fromUtc, timeZone);
        var toLocal = TimeZoneInfo.ConvertTimeFromUtc(toUtc, timeZone).AddDays(-1);
        var period = fromLocal.Date == toLocal.Date
            ? S["{0:D}", fromLocal].Value
            : S["{0:d} to {1:d}", fromLocal, toLocal].Value;

        var html = $"<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#212529\"><h2 style=\"font-size:18px;margin:0 0 4px 0\">{WebUtility.HtmlEncode(title)}</h2>" +
            $"<p style=\"color:#57606a;margin:0 0 16px 0\">{WebUtility.HtmlEncode(period)}</p>" +
            ReportWorkflowService.ToHtml(new ReportRunResult { Document = document }) +
            "</div>";

        var message = new MailMessage
        {
            To = subscription.Recipients,
            Subject = S["{0}: {1}", title, period],
            HtmlBody = html,
        };

        MemoryStream attachment = null;

        try
        {
            if (hasAttachment && _exports.GetDescriptor(subscription.AttachmentFormat) is { } format)
            {
                attachment = new MemoryStream(_exports.GetFormat(format.Name).Serialize(document));
                message.Attachments.Add(new MailMessageAttachment
                {
                    Filename = $"{descriptor.Name}-{fromLocal:yyyyMMdd}-to-{toLocal:yyyyMMdd}{format.Extension}",
                    Stream = attachment,
                });
            }

            var result = await _emailService.SendAsync(message, cancellationToken: cancellationToken);

            return result.Succeeded ? null : S["The email couldn't be sent: {0}", string.Join("; ", result.Errors.Select(error => error.Message))];
        }
        finally
        {
            attachment?.Dispose();
        }
    }
}
