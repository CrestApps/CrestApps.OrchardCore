using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using CrestApps.Core.SignalR.Services;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ContactCenterSoftPhoneWidgetDisplayDriver : DisplayDriver<SoftPhoneWidget>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;
    private readonly IAgentStateReasonCodeManager _reasonCodeManager;
    private readonly HubRouteManager _hubRouteManager;
    private readonly IResourceManager _resourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSoftPhoneWidgetDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="agentProfileManager">The agent profile manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="hubRouteManager">The SignalR hub route manager.</param>
    /// <param name="resourceManager">The Orchard resource manager.</param>
    /// <param name="reasonCodeManagers">The optional agent state reason code managers, available when the Agents feature is enabled.</param>
    public ContactCenterSoftPhoneWidgetDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAgentProfileManager agentProfileManager,
        IActivityQueueManager queueManager,
        ContactCenterAdminFormOptionsProvider optionsProvider,
        HubRouteManager hubRouteManager,
        IResourceManager resourceManager,
        IEnumerable<IAgentStateReasonCodeManager> reasonCodeManagers)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _agentProfileManager = agentProfileManager;
        _queueManager = queueManager;
        _optionsProvider = optionsProvider;
        _hubRouteManager = hubRouteManager;
        _resourceManager = resourceManager;
        _reasonCodeManager = reasonCodeManagers.FirstOrDefault();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> DisplayAsync(SoftPhoneWidget widget, BuildDisplayContext context)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true ||
            !await _authorizationService.AuthorizeAsync(user, ContactCenterPermissions.SignIntoQueues))
        {
            return null;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var profile = await _agentProfileManager.FindByUserIdAsync(userId);
        var allowedQueueIds = new HashSet<string>(profile?.AllowedQueueIds ?? [], StringComparer.OrdinalIgnoreCase);
        var allowedCampaignIds = new HashSet<string>(profile?.AllowedCampaignIds ?? [], StringComparer.OrdinalIgnoreCase);
        var selectedCampaignIds = AgentEntitlementUtilities.FilterEntitled(profile?.CampaignIds, profile?.AllowedCampaignIds);
        var queues = await _queueManager.ListEnabledAsync();
        var entitledQueues = queues.Where(queue => allowedQueueIds.Contains(queue.ItemId)).ToList();
        var campaignOptions = await _optionsProvider.GetCampaignOptionsAsync(selectedCampaignIds);
        var entitledCampaignOptions = campaignOptions.Where(option => allowedCampaignIds.Contains(option.Value)).ToList();
        var reasonCodes = _reasonCodeManager is null
            ? []
            : await _reasonCodeManager.ListEnabledAsync();

        _resourceManager.RegisterResource("stylesheet", "crestapps-bootstrap-select").AtHead();
        _resourceManager.RegisterResource("script", "contact-center-realtime").AtFoot();
        _resourceManager.RegisterResource("script", "contact-center-soft-phone").AtFoot();

        var viewModel = new AgentSoftPhoneViewModel
        {
            HubUrl = _hubRouteManager.GetPathByHub<ContactCenterHub>(),
            Profile = profile,
            AvailableQueues = entitledQueues,
            SelectedQueueIds = AgentEntitlementUtilities.FilterEntitled(profile?.QueueIds, profile?.AllowedQueueIds),
            CampaignOptions = entitledCampaignOptions,
            SelectedCampaignIds = selectedCampaignIds,
            ReasonCodes = [.. reasonCodes],
        };

        return Combine(
            View("ContactCenterSoftPhonePresence_Header", viewModel)
                .Location("Detail", "HeaderActions:5"),
            View("ContactCenterSoftPhoneWork_Tab", viewModel)
                .Location("Detail", "Tabs:10"),
            View("ContactCenterSoftPhoneWork_View", viewModel)
                .Location("Detail", "Views:10")
        );
    }
}
