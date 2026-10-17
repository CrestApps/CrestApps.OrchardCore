using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds campaign, channel, source, status, and disposition filters to Omnichannel reports, and a page picker to the
/// reports that page their rows. A report that declares its filters through <see cref="IReportFilterMetadata"/> gets
/// the filters only when it names <see cref="FilterName"/>, and the page picker only when it names
/// <see cref="PageFilterName"/>. A report that implements <see cref="IOmnichannelReportFilterDefaults"/> has its
/// defaults applied the first time it is opened, before the filter form has been submitted.
/// </summary>
public sealed class OmnichannelReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ICatalogManager<OmnichannelCampaignGroup> _campaignGroupManager;
    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositionManager;
    private readonly IReportManager _reportManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ActivitySourceOptions _activitySourceOptions;
    private readonly ActivityChannelOptions _activityChannelOptions;

    /// <summary>
    /// The filter name a report lists in <see cref="IReportFilterMetadata.FilterNames"/> to get these filters.
    /// </summary>
    public const string FilterName = "OmnichannelActivity";

    /// <summary>
    /// The filter name a report lists in <see cref="IReportFilterMetadata.FilterNames"/> to get the page picker.
    /// </summary>
    public const string PageFilterName = "OmnichannelActivityPage";

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelReportFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="campaignManager">The campaign manager.</param>
    /// <param name="campaignGroupManager">The campaign group manager.</param>
    /// <param name="dispositionManager">The disposition manager.</param>
    /// <param name="reportManager">The report manager.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, used to tell a first opening from a submitted filter form.</param>
    /// <param name="activitySourceOptions">The activity sources registered by the enabled features.</param>
    /// <param name="activityChannelOptions">The activity channels registered by the enabled features.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelReportFilterDisplayDriver(
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ICatalogManager<OmnichannelCampaignGroup> campaignGroupManager,
        INamedCatalogManager<OmnichannelDisposition> dispositionManager,
        IReportManager reportManager,
        IHttpContextAccessor httpContextAccessor,
        IOptions<ActivitySourceOptions> activitySourceOptions,
        IOptions<ActivityChannelOptions> activityChannelOptions,
        IStringLocalizer<OmnichannelReportFilterDisplayDriver> stringLocalizer)
    {
        _campaignManager = campaignManager;
        _campaignGroupManager = campaignGroupManager;
        _dispositionManager = dispositionManager;
        _reportManager = reportManager;
        _httpContextAccessor = httpContextAccessor;
        _activitySourceOptions = activitySourceOptions.Value;
        _activityChannelOptions = activityChannelOptions.Value;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (!AppliesTo(filter.ReportName))
        {
            return null;
        }

        return Initialize<OmnichannelReportFilterViewModel>("OmnichannelReportFilter_Edit", async model =>
        {
            await PopulateAsync(model, filter);
        }).Location("Content:2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ReportFilter filter, UpdateEditorContext context)
    {
        if (!AppliesTo(filter.ReportName))
        {
            return null;
        }

        var model = new OmnichannelReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        filter.Set(OmnichannelReportFilter.CampaignId, model.CampaignId);
        filter.Set(OmnichannelReportFilter.CampaignGroupId, model.CampaignGroupId);
        filter.Set(OmnichannelReportFilter.Channel, model.Channel);
        filter.Set(OmnichannelReportFilter.Source, model.Source);
        filter.Set(OmnichannelReportFilter.Status, model.Status);
        filter.Set(OmnichannelReportFilter.DispositionIds, GetSelectedDispositionIds(model.DispositionIds));
        filter.Set<int?>(OmnichannelReportFilter.Page, model.Page > 1 ? model.Page : null);

        // Until the filter form has been submitted once, nothing has been chosen, and a report may start with its
        // own defaults. Once it has, an empty value is a choice ("All sources") and is kept.
        if (!WasSubmitted() && _reportManager.GetReport(filter.ReportName) is IOmnichannelReportFilterDefaults defaults)
        {
            defaults.ApplyDefaults(filter);
        }

        return Edit(filter, context);
    }

    private bool AppliesTo(string reportName)
    {
        if (reportName?.StartsWith("omnichannel-", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return _reportManager.GetReport(reportName) is not IReportFilterMetadata metadata ||
            metadata.FilterNames.Contains(FilterName, StringComparer.Ordinal);
    }

    private bool PagesRows(string reportName)
    {
        return _reportManager.GetReport(reportName) is IReportFilterMetadata metadata &&
            metadata.FilterNames.Contains(PageFilterName, StringComparer.Ordinal);
    }

    private bool WasSubmitted()
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        if (request is null)
        {
            // There is no form to inspect, so the filter is taken as it is.
            return true;
        }

        // The Source list is always part of the form, so a submitted form carries its field even for "All sources".
        var sourceField = nameof(OmnichannelReportFilterViewModel.Source);
        var key = string.IsNullOrEmpty(Prefix)
            ? sourceField
            : $"{Prefix}.{sourceField}";

        return request.Query.ContainsKey(key);
    }

    private static string[] GetSelectedDispositionIds(string[] dispositionIds)
    {
        if (dispositionIds is null)
        {
            return null;
        }

        var selected = dispositionIds
            .Where(dispositionId => !string.IsNullOrWhiteSpace(dispositionId))
            .Select(dispositionId => dispositionId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return selected.Length > 0
            ? selected
            : null;
    }

    private async Task PopulateAsync(OmnichannelReportFilterViewModel model, ReportFilter filter)
    {
        model.CampaignId = filter.GetOrDefault<string>(OmnichannelReportFilter.CampaignId);
        model.CampaignGroupId = filter.GetOrDefault<string>(OmnichannelReportFilter.CampaignGroupId);
        model.Channel = filter.GetOrDefault<string>(OmnichannelReportFilter.Channel);
        model.Source = filter.GetOrDefault<string>(OmnichannelReportFilter.Source);
        model.Status = filter.GetOrDefault<string>(OmnichannelReportFilter.Status);
        model.DispositionIds = filter.GetOrDefault<string[]>(OmnichannelReportFilter.DispositionIds) ?? [];
        model.Page = filter.TryGet<int>(OmnichannelReportFilter.Page, out var page) && page > 1
            ? page
            : 1;
        model.ShowPage = PagesRows(filter.ReportName);

        var campaigns = await _campaignManager.GetAllAsync();

        model.Campaigns = campaigns
            .OrderBy(campaign => campaign.DisplayText)
            .Select(campaign => new SelectListItem(campaign.DisplayText ?? campaign.ItemId, campaign.ItemId))
            .ToList();

        var campaignGroups = await _campaignGroupManager.GetAllAsync();

        model.CampaignGroups = campaignGroups
            .OrderBy(group => group.DisplayText)
            .Select(group => new SelectListItem(group.DisplayText ?? group.ItemId, group.ItemId))
            .ToList();

        var dispositions = await _dispositionManager.GetAllAsync();

        model.Dispositions = dispositions
            .OrderBy(disposition => disposition.Name)
            .Select(disposition => new SelectListItem(disposition.Name ?? disposition.ItemId, disposition.ItemId))
            .ToList();

        // Only the channels and sources the enabled features can put on activities are offered. A value saved
        // before, such as a channel no feature creates, stays listed so the filter still shows what it applies.
        model.Channels = ActivityFilterSelectListBuilder.BuildChannelItems(_activityChannelOptions, model.Channel);
        model.Sources = ActivityFilterSelectListBuilder.BuildSourceItems(_activitySourceOptions, model.Source);

        model.Statuses =
        [
            new SelectListItem(S["Not started"], ActivityStatus.NotStated.ToString()),
            new SelectListItem(S["Awaiting agent response"], ActivityStatus.AwaitingAgentResponse.ToString()),
            new SelectListItem(S["Awaiting customer answer"], ActivityStatus.AwaitingCustomerAnswer.ToString()),
            new SelectListItem(S["Completed"], ActivityStatus.Completed.ToString()),
            new SelectListItem(S["Pending"], ActivityStatus.Pending.ToString()),
            new SelectListItem(S["Scheduled"], ActivityStatus.Scheduled.ToString()),
            new SelectListItem(S["Reserved"], ActivityStatus.Reserved.ToString()),
            new SelectListItem(S["Dialing"], ActivityStatus.Dialing.ToString()),
            new SelectListItem(S["In progress"], ActivityStatus.InProgress.ToString()),
            new SelectListItem(S["Failed"], ActivityStatus.Failed.ToString()),
            new SelectListItem(S["Cancelled"], ActivityStatus.Cancelled.ToString()),
            new SelectListItem(S["Purged"], ActivityStatus.Purged.ToString()),
        ];
    }
}
