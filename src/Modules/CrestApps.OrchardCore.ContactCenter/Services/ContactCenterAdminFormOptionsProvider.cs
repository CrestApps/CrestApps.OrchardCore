using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides select-list options used by Contact Center admin forms.
/// </summary>
public sealed class ContactCenterAdminFormOptionsProvider
{
    private readonly ICatalogManager<OmnichannelCampaign> _campaignManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IOmnichannelChannelEndpointManager _channelEndpointManager;
    private readonly IEnumerable<IContactCenterVoiceProvider> _voiceProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAdminFormOptionsProvider"/> class.
    /// </summary>
    /// <param name="campaignManager">The omnichannel campaign manager.</param>
    /// <param name="queueManager">The activity queue manager.</param>
    /// <param name="agentProfileManager">The agent profile manager.</param>
    /// <param name="channelEndpointManager">The omnichannel channel endpoint manager.</param>
    /// <param name="voiceProviders">The registered voice call providers.</param>
    public ContactCenterAdminFormOptionsProvider(
        ICatalogManager<OmnichannelCampaign> campaignManager,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentProfileManager,
        IOmnichannelChannelEndpointManager channelEndpointManager,
        IEnumerable<IContactCenterVoiceProvider> voiceProviders)
    {
        _campaignManager = campaignManager;
        _queueManager = queueManager;
        _agentProfileManager = agentProfileManager;
        _channelEndpointManager = channelEndpointManager;
        _voiceProviders = voiceProviders;
    }

    internal async Task<IList<SelectListItem>> GetCampaignOptionsAsync(IEnumerable<string> selectedCampaignIds)
    {
        var selected = CreateSelectedSet(selectedCampaignIds, StringComparer.Ordinal);
        var campaigns = await _campaignManager.GetAllAsync();

        var options = campaigns
            .OrderBy(campaign => campaign.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .Select(campaign => new SelectListItem(GetCampaignText(campaign), campaign.ItemId, selected.Contains(campaign.ItemId)))
            .ToList();

        AddMissingSelectedOptions(options, selected, StringComparer.Ordinal);

        return options;
    }

    internal async Task<IList<SelectListItem>> GetQueueOptionsAsync(string selectedQueueId)
    {
        var selected = CreateSelectedSet([selectedQueueId], StringComparer.Ordinal);
        var queues = await _queueManager.GetAllAsync();

        var options = queues
            .OrderBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(queue => new SelectListItem(queue.Name ?? queue.ItemId, queue.ItemId, selected.Contains(queue.ItemId)))
            .ToList();

        AddMissingSelectedOptions(options, selected, StringComparer.Ordinal);

        return options;
    }

    internal async Task<IList<SelectListItem>> GetSkillOptionsAsync(IEnumerable<string> selectedSkills)
    {
        var selected = CreateSelectedSet(selectedSkills, StringComparer.OrdinalIgnoreCase);
        var queues = await _queueManager.GetAllAsync();
        var agents = await _agentProfileManager.GetAllAsync();

        var skills = queues
            .SelectMany(queue => queue.RequiredSkills)
            .Concat(agents.SelectMany(agent => agent.Skills))
            .Concat(selected)
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(skill => skill, StringComparer.CurrentCultureIgnoreCase);

        return skills
            .Select(skill => new SelectListItem(skill, skill, selected.Contains(skill)))
            .ToArray();
    }

    internal async Task<IList<SelectListItem>> GetInboundChannelEndpointOptionsAsync(string selectedEndpointId)
    {
        var selected = CreateSelectedSet([selectedEndpointId], StringComparer.Ordinal);
        var endpoints = await _channelEndpointManager.GetAllAsync();

        var options = endpoints
            .OrderBy(endpoint => endpoint.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .Select(endpoint => new SelectListItem(GetEndpointText(endpoint), endpoint.ItemId, selected.Contains(endpoint.ItemId)))
            .ToList();

        AddMissingSelectedOptions(options, selected, StringComparer.Ordinal);

        return options;
    }

    internal IList<SelectListItem> GetVoiceProviderOptions(string selectedProviderName)
    {
        var selected = CreateSelectedSet([selectedProviderName], StringComparer.OrdinalIgnoreCase);
        var options = _voiceProviders
            .OrderBy(provider => provider.Name.Value, StringComparer.CurrentCultureIgnoreCase)
            .Select(provider => new SelectListItem(provider.Name.Value, provider.TechnicalName, selected.Contains(provider.TechnicalName)))
            .ToList();

        AddMissingSelectedOptions(options, selected, StringComparer.OrdinalIgnoreCase);

        return options;
    }

    internal async Task PopulateQueueEditorAsync(QueueViewModel model)
    {
        model.RequiredSkills = ContactCenterFormHelpers.NormalizeList(model.RequiredSkills);
        model.SkillOptions = await GetSkillOptionsAsync(model.RequiredSkills);
        model.InboundChannelEndpointOptions = await GetInboundChannelEndpointOptionsAsync(model.InboundChannelEndpointId);
    }

    internal async Task PopulateDialerProfileEditorAsync(DialerProfileViewModel model)
    {
        model.CampaignOptions = await GetCampaignOptionsAsync([model.CampaignId]);
        model.QueueOptions = await GetQueueOptionsAsync(model.QueueId);
        model.ProviderOptions = GetVoiceProviderOptions(model.ProviderName);
    }

    private static HashSet<string> CreateSelectedSet(IEnumerable<string> values, StringComparer comparer)
    {
        return values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(comparer);
    }

    private static string GetCampaignText(OmnichannelCampaign campaign)
    {
        return string.IsNullOrWhiteSpace(campaign.DisplayText)
            ? campaign.ItemId
            : campaign.DisplayText;
    }

    private static string GetEndpointText(OmnichannelChannelEndpoint endpoint)
    {
        var displayText = string.IsNullOrWhiteSpace(endpoint.DisplayText)
            ? endpoint.ItemId
            : endpoint.DisplayText;

        if (!string.IsNullOrWhiteSpace(endpoint.Channel) && !string.IsNullOrWhiteSpace(endpoint.Value))
        {
            return $"{displayText} ({endpoint.Channel}: {endpoint.Value})";
        }

        return displayText;
    }

    private static void AddMissingSelectedOptions(List<SelectListItem> options, IEnumerable<string> selectedValues, StringComparer comparer)
    {
        var existingValues = options
            .Select(option => option.Value)
            .ToHashSet(comparer);

        foreach (var selectedValue in selectedValues)
        {
            if (!existingValues.Contains(selectedValue))
            {
                options.Add(new SelectListItem(selectedValue, selectedValue, selected: true));
            }
        }
    }
}
