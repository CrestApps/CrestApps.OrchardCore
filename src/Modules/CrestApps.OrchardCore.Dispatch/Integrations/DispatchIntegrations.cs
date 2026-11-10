using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.Email;
using OrchardCore.Settings;
using OrchardCore.Sms;
using YesSql;
using ISession = YesSql.ISession;
using AssetRecord = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Dispatch.Integrations;

/// <summary>
/// Names the business after the site, and points tracking links at this site.
/// </summary>
internal sealed class SiteDispatchSiteInfo : IDispatchSiteInfo
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SiteDispatchSiteInfo(ISiteService siteService, IHttpContextAccessor httpContextAccessor)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> CompanyNameAsync() => (await _siteService.GetSiteSettingsAsync()).SiteName;

    // The site's base address when set, else the request's, which background tasks don't have.
    public async Task<string> TrackingUrlAsync(string token)
    {
        var baseUrl = (await _siteService.GetSiteSettingsAsync()).BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl) && _httpContextAccessor.HttpContext?.Request is { } request)
        {
            baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        }

        return string.IsNullOrWhiteSpace(baseUrl) ? null : $"{baseUrl.TrimEnd('/')}/track/{Uri.EscapeDataString(token)}";
    }
}

/// <summary>
/// Texts customers through the site's SMS provider.
/// </summary>
internal sealed class SmsVisitNoticeSender : IVisitNoticeSender
{
    private readonly ISmsService _smsService;

    public SmsVisitNoticeSender(ISmsService smsService)
    {
        _smsService = smsService;
    }

    public string Channel => "Sms";

    public bool CanSend(VisitNoticeMessage message) => !string.IsNullOrWhiteSpace(message?.Phone);

    public async Task<string> SendAsync(VisitNoticeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var result = await _smsService.SendAsync(new SmsMessage { To = message.Phone, Body = message.Text }, cancellationToken);

        return result.Succeeded ? null : string.Join(" ", result.Errors.Select(error => error.Message));
    }
}

/// <summary>
/// Writes to customers through the site's email provider.
/// </summary>
internal sealed class EmailVisitNoticeSender : IVisitNoticeSender
{
    private readonly IEmailService _emailService;

    public EmailVisitNoticeSender(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public string Channel => "Email";

    public bool CanSend(VisitNoticeMessage message) => !string.IsNullOrWhiteSpace(message?.Email);

    public async Task<string> SendAsync(VisitNoticeMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var result = await _emailService.SendAsync(new MailMessage { To = message.Email, Subject = message.Subject, TextBody = message.Text }, cancellationToken: cancellationToken);

        return result.Succeeded ? null : string.Join(" ", result.Errors.Select(error => error.Message));
    }
}

/// <summary>
/// Books Fleet's schedulable equipment for visits that need it.
/// </summary>
internal sealed class FleetDispatchEquipmentService : IDispatchEquipmentService
{
    private readonly ISession _session;
    private readonly AssetTypeStore _types;

    public FleetDispatchEquipmentService(ISession session, AssetTypeStore types)
    {
        _session = session;
        _types = types;
    }

    public async Task<IReadOnlyList<DispatchEquipment>> ListAsync(string assetTypeId, CancellationToken cancellationToken = default)
    {
        var active = nameof(AssetStatus.Active);
        var assets = await _session.Query<AssetRecord, AssetIndex>(index => index.AssetTypeId == assetTypeId && index.Status == active, collection: AssetsConstants.CollectionName).ListAsync(cancellationToken);

        return [.. assets
            .Where(asset => asset.Fleet is { Schedulable: true } && !string.IsNullOrEmpty(asset.Fleet.CalendarId))
            .Select(asset => new DispatchEquipment(asset.ItemId, asset.Name, asset.Fleet.CalendarId))];
    }

    public async Task<string> TypeNameAsync(string assetTypeId, CancellationToken cancellationToken = default)
        => (await _types.FindByIdAsync(assetTypeId, cancellationToken))?.Name;
}

/// <summary>
/// Flags the visits at risk every five minutes.
/// </summary>
[BackgroundTask(Title = "Dispatch: visits at risk", Schedule = "*/5 * * * *", Description = "Flags visits running over, not started, arriving after their window, or with a technician on time off.")]
public sealed class VisitRiskBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<VisitRiskMonitor>().RunAsync(cancellationToken), "at_risk"));
}

/// <summary>
/// Sends the reminders of tomorrow's visits, every fifteen minutes from the reminder hour.
/// </summary>
[BackgroundTask(Title = "Dispatch: visit reminders", Schedule = "*/15 * * * *", Description = "Reminds customers of tomorrow's visits once it is the reminder hour.")]
public sealed class VisitReminderBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<VisitNoticeService>().SendRemindersAsync(cancellationToken), "sent"));
}
