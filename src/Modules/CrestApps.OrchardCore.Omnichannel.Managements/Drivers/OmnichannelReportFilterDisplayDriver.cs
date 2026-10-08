using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds campaign, channel, source, and status filters to Omnichannel reports. A report that declares its filters
/// through <see cref="IReportFilterMetadata"/> gets them only when it names <see cref="FilterName"/>.
/// </summary>
public sealed class OmnichannelReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ICatalogManager<OmnichannelCampaignGroup> _campaignGroupManager;
    private readonly IReportManager _reportManager;
    private readonly ActivitySourceOptions _activitySourceOptions;
    private readonly ActivityChannelOptions _activityChannelOptions;

    /// <summary>
    /// The filter name a report lists in <see cref="IReportFilterMetadata.FilterNames"/> to get these filters.
    /// </summary>
    public const string FilterName = "OmnichannelActivity";

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelReportFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="campaignManager">The campaign manager.</param>
    /// <param name="campaignGroupManager">The campaign group manager.</param>
    /// <param name="reportManager">The report manager.</param>
    /// <param name="activitySourceOptions">The activity sources registered by the enabled features.</param>
    /// <param name="activityChannelOptions">The activity channels registered by the enabled features.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelReportFilterDisplayDriver(
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ICatalogManager<OmnichannelCampaignGroup> campaignGroupManager,
        IReportManager reportManager,
        IOptions<ActivitySourceOptions> activitySourceOptions,
        IOptions<ActivityChannelOptions> activityChannelOptions,
        IStringLocalizer<OmnichannelReportFilterDisplayDriver> stringLocalizer)
    {
        _campaignManager = campaignManager;
        _campaignGroupManager = campaignGroupManager;
        _reportManager = reportManager;
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

        return Edit(filter, context);
    }

    private bool AppliesTo(string reportName)
    {
        if (reportName?.StartsWith("omnichannel-", StringComparison.Ordinal) != true)
        {
            return false;
        }

        return _reportManager.FindByName(reportName) is not IReportFilterMetadata metadata ||
            metadata.FilterNames.Contains(FilterName, StringComparer.Ordinal);
    }

    private async Task PopulateAsync(OmnichannelReportFilterViewModel model, ReportFilter filter)
    {
        model.CampaignId = filter.GetOrDefault<string>(OmnichannelReportFilter.CampaignId);
        model.CampaignGroupId = filter.GetOrDefault<string>(OmnichannelReportFilter.CampaignGroupId);
        model.Channel = filter.GetOrDefault<string>(OmnichannelReportFilter.Channel);
        model.Source = filter.GetOrDefault<string>(OmnichannelReportFilter.Source);
        model.Status = filter.GetOrDefault<string>(OmnichannelReportFilter.Status);

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
