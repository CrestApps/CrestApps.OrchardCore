using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Reports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

/// <summary>
/// Adds campaign, channel, source, and status filters to Omnichannel reports.
/// </summary>
public sealed class OmnichannelReportFilterDisplayDriver : DisplayDriver<ReportFilter>
{
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly ICatalogManager<OmnichannelCampaignGroup> _campaignGroupManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelReportFilterDisplayDriver"/> class.
    /// </summary>
    /// <param name="campaignManager">The campaign manager.</param>
    /// <param name="campaignGroupManager">The campaign group manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelReportFilterDisplayDriver(
        ICatalogManager<OmnichannelCampaign> campaignManager,
        ICatalogManager<OmnichannelCampaignGroup> campaignGroupManager,
        IStringLocalizer<OmnichannelReportFilterDisplayDriver> stringLocalizer)
    {
        _campaignManager = campaignManager;
        _campaignGroupManager = campaignGroupManager;
        S = stringLocalizer;
    }

    private IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override IDisplayResult Edit(ReportFilter filter, BuildEditorContext context)
    {
        if (filter.ReportName?.StartsWith("omnichannel-", StringComparison.Ordinal) != true)
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
        if (filter.ReportName?.StartsWith("omnichannel-", StringComparison.Ordinal) != true)
        {
            return null;
        }

        var model = new OmnichannelReportFilterViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        OmnichannelReportFilter.SetString(filter, OmnichannelReportFilter.CampaignId, model.CampaignId);
        OmnichannelReportFilter.SetString(filter, OmnichannelReportFilter.CampaignGroupId, model.CampaignGroupId);
        OmnichannelReportFilter.SetString(filter, OmnichannelReportFilter.Channel, model.Channel);
        OmnichannelReportFilter.SetString(filter, OmnichannelReportFilter.Source, model.Source);
        OmnichannelReportFilter.SetString(filter, OmnichannelReportFilter.Status, model.Status);

        return Edit(filter, context);
    }

    private async Task PopulateAsync(OmnichannelReportFilterViewModel model, ReportFilter filter)
    {
        model.CampaignId = OmnichannelReportFilter.GetString(filter, OmnichannelReportFilter.CampaignId);
        model.CampaignGroupId = OmnichannelReportFilter.GetString(filter, OmnichannelReportFilter.CampaignGroupId);
        model.Channel = OmnichannelReportFilter.GetString(filter, OmnichannelReportFilter.Channel);
        model.Source = OmnichannelReportFilter.GetString(filter, OmnichannelReportFilter.Source);
        model.Status = OmnichannelReportFilter.GetString(filter, OmnichannelReportFilter.Status);

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

        model.Channels =
        [
            new SelectListItem(S["Phone"], OmnichannelConstants.Channels.Phone),
            new SelectListItem(S["SMS"], OmnichannelConstants.Channels.Sms),
            new SelectListItem(S["Email"], OmnichannelConstants.Channels.Email),
            new SelectListItem(S["Chat"], "Chat"),
        ];

        model.Sources =
        [
            new SelectListItem(S["Manual"], ActivitySources.Manual),
            new SelectListItem(S["Automatic"], ActivitySources.Automatic),
            new SelectListItem(S["Dialer"], ActivitySources.Dialer),
            new SelectListItem(S["Preview dial"], ActivitySources.PreviewDial),
            new SelectListItem(S["Power dial"], ActivitySources.PowerDial),
            new SelectListItem(S["Progressive dial"], ActivitySources.ProgressiveDial),
            new SelectListItem(S["Predictive dial"], ActivitySources.PredictiveDial),
            new SelectListItem(S["Callback"], ActivitySources.Callback),
            new SelectListItem(S["Inbound"], ActivitySources.Inbound),
            new SelectListItem(S["Workflow"], ActivitySources.Workflow),
            new SelectListItem(S["API"], ActivitySources.Api),
        ];

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
