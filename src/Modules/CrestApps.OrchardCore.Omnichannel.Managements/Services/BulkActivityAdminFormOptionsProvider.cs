using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Provides select-list options used by the bulk activity management filters and actions.
/// </summary>
public sealed class BulkActivityAdminFormOptionsProvider
{
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BulkActivityAdminFormOptionsProvider"/> class.
    /// </summary>
    /// <param name="campaignManager">The omnichannel campaign manager.</param>
    /// <param name="serviceProvider">The service provider used for optional dialer services.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BulkActivityAdminFormOptionsProvider(
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IServiceProvider serviceProvider,
        IStringLocalizer<BulkActivityAdminFormOptionsProvider> stringLocalizer)
    {
        _campaignManager = campaignManager;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    internal IList<SelectListItem> GetSourceOptions(
        string selectedSource,
        string emptyText)
    {
        return
        [
            new SelectListItem(S[emptyText], string.Empty, string.IsNullOrEmpty(selectedSource)),
            new SelectListItem(S["Manual"], ActivitySources.Manual, string.Equals(selectedSource, ActivitySources.Manual, StringComparison.Ordinal)),
            new SelectListItem(S["Automatic"], ActivitySources.Automatic, string.Equals(selectedSource, ActivitySources.Automatic, StringComparison.Ordinal)),
            new SelectListItem(S["Dialer"], ActivitySources.Dialer, string.Equals(selectedSource, ActivitySources.Dialer, StringComparison.Ordinal)),
            new SelectListItem(S["Preview dial"], ActivitySources.PreviewDial, string.Equals(selectedSource, ActivitySources.PreviewDial, StringComparison.Ordinal)),
            new SelectListItem(S["Power dial"], ActivitySources.PowerDial, string.Equals(selectedSource, ActivitySources.PowerDial, StringComparison.Ordinal)),
            new SelectListItem(S["Progressive dial"], ActivitySources.ProgressiveDial, string.Equals(selectedSource, ActivitySources.ProgressiveDial, StringComparison.Ordinal)),
            new SelectListItem(S["Predictive dial"], ActivitySources.PredictiveDial, string.Equals(selectedSource, ActivitySources.PredictiveDial, StringComparison.Ordinal)),
            new SelectListItem(S["Callback"], ActivitySources.Callback, string.Equals(selectedSource, ActivitySources.Callback, StringComparison.Ordinal)),
            new SelectListItem(S["Inbound"], ActivitySources.Inbound, string.Equals(selectedSource, ActivitySources.Inbound, StringComparison.Ordinal)),
            new SelectListItem(S["Workflow"], ActivitySources.Workflow, string.Equals(selectedSource, ActivitySources.Workflow, StringComparison.Ordinal)),
            new SelectListItem(S["API"], ActivitySources.Api, string.Equals(selectedSource, ActivitySources.Api, StringComparison.Ordinal)),
        ];
    }

    internal IList<SelectListItem> GetInteractionTypeOptions(string selectedInteractionType, string emptyText)
    {
        return
        [
            new SelectListItem(S[emptyText], string.Empty, string.IsNullOrEmpty(selectedInteractionType)),
            new SelectListItem(S["Manual"], nameof(ActivityInteractionType.Manual), string.Equals(selectedInteractionType, nameof(ActivityInteractionType.Manual), StringComparison.Ordinal)),
            new SelectListItem(S["Automated"], nameof(ActivityInteractionType.Automated), string.Equals(selectedInteractionType, nameof(ActivityInteractionType.Automated), StringComparison.Ordinal)),
        ];
    }

    internal IList<SelectListItem> GetStatusOptions(string selectedStatus, string emptyText)
    {
        return
        [
            new SelectListItem(S[emptyText], string.Empty, string.IsNullOrEmpty(selectedStatus)),
            new SelectListItem(S["Not started"], nameof(ActivityStatus.NotStated), string.Equals(selectedStatus, nameof(ActivityStatus.NotStated), StringComparison.Ordinal)),
            new SelectListItem(S["Scheduled"], nameof(ActivityStatus.Scheduled), string.Equals(selectedStatus, nameof(ActivityStatus.Scheduled), StringComparison.Ordinal)),
            new SelectListItem(S["Pending"], nameof(ActivityStatus.Pending), string.Equals(selectedStatus, nameof(ActivityStatus.Pending), StringComparison.Ordinal)),
            new SelectListItem(S["Awaiting agent response"], nameof(ActivityStatus.AwaitingAgentResponse), string.Equals(selectedStatus, nameof(ActivityStatus.AwaitingAgentResponse), StringComparison.Ordinal)),
            new SelectListItem(S["Failed"], nameof(ActivityStatus.Failed), string.Equals(selectedStatus, nameof(ActivityStatus.Failed), StringComparison.Ordinal)),
            new SelectListItem(S["Cancelled"], nameof(ActivityStatus.Cancelled), string.Equals(selectedStatus, nameof(ActivityStatus.Cancelled), StringComparison.Ordinal)),
        ];
    }

    internal IList<SelectListItem> GetAssignmentStatusOptions(string selectedAssignmentStatus, string emptyText)
    {
        return
        [
            new SelectListItem(S[emptyText], string.Empty, string.IsNullOrEmpty(selectedAssignmentStatus)),
            new SelectListItem(S["Unassigned"], nameof(ActivityAssignmentStatus.Unassigned), string.Equals(selectedAssignmentStatus, nameof(ActivityAssignmentStatus.Unassigned), StringComparison.Ordinal)),
            new SelectListItem(S["Available"], nameof(ActivityAssignmentStatus.Available), string.Equals(selectedAssignmentStatus, nameof(ActivityAssignmentStatus.Available), StringComparison.Ordinal)),
            new SelectListItem(S["Reserved"], nameof(ActivityAssignmentStatus.Reserved), string.Equals(selectedAssignmentStatus, nameof(ActivityAssignmentStatus.Reserved), StringComparison.Ordinal)),
            new SelectListItem(S["Assigned"], nameof(ActivityAssignmentStatus.Assigned), string.Equals(selectedAssignmentStatus, nameof(ActivityAssignmentStatus.Assigned), StringComparison.Ordinal)),
            new SelectListItem(S["In progress"], nameof(ActivityAssignmentStatus.InProgress), string.Equals(selectedAssignmentStatus, nameof(ActivityAssignmentStatus.InProgress), StringComparison.Ordinal)),
            new SelectListItem(S["Released"], nameof(ActivityAssignmentStatus.Released), string.Equals(selectedAssignmentStatus, nameof(ActivityAssignmentStatus.Released), StringComparison.Ordinal)),
        ];
    }

    internal async Task<IList<SelectListItem>> GetCampaignOptionsAsync(string selectedCampaignId, string emptyText)
    {
        var campaigns = await _campaignManager.GetAllAsync();
        var options = campaigns
            .OrderBy(campaign => campaign.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .Select(campaign => new SelectListItem(GetCampaignText(campaign), campaign.ItemId, string.Equals(selectedCampaignId, campaign.ItemId, StringComparison.Ordinal)))
            .ToList();

        options.Insert(0, new SelectListItem(S[emptyText], string.Empty, string.IsNullOrEmpty(selectedCampaignId)));

        if (!string.IsNullOrWhiteSpace(selectedCampaignId) &&
            options.All(option => !string.Equals(option.Value, selectedCampaignId, StringComparison.Ordinal)))
        {
            options.Add(new SelectListItem(selectedCampaignId, selectedCampaignId, selected: true));
        }

        return options;
    }

    internal async Task<IList<SelectListItem>> GetDialerProfileOptionsAsync(string selectedDialerProfileId, string emptyText)
    {
        var dialerProfileManager = _serviceProvider.GetService<IDialerProfileManager>();

        if (dialerProfileManager is null)
        {
            return
            [
                new SelectListItem(S[emptyText], string.Empty, selected: true),
            ];
        }

        var profiles = await dialerProfileManager.GetAllAsync();
        var options = profiles
            .OrderBy(profile => profile.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(profile => new SelectListItem(profile.Name ?? profile.ItemId, profile.ItemId, string.Equals(selectedDialerProfileId, profile.ItemId, StringComparison.Ordinal)))
            .ToList();

        options.Insert(0, new SelectListItem(S[emptyText], string.Empty, string.IsNullOrEmpty(selectedDialerProfileId)));

        if (!string.IsNullOrWhiteSpace(selectedDialerProfileId) &&
            options.All(option => !string.Equals(option.Value, selectedDialerProfileId, StringComparison.Ordinal)))
        {
            options.Add(new SelectListItem(selectedDialerProfileId, selectedDialerProfileId, selected: true));
        }

        return options;
    }

    private static string GetCampaignText(OmnichannelCampaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.DisplayText)
            ? campaign.ItemId
            : campaign.DisplayText;
    }
}
