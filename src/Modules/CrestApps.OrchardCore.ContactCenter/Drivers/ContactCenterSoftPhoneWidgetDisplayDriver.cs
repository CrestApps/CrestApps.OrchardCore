using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ContactCenterSoftPhoneWidgetDisplayDriver : DisplayDriver<SoftPhoneWidget>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterSoftPhoneWidgetDisplayDriver"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="agentProfileManager">The agent profile manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="optionsProvider">The admin form options provider.</param>
    public ContactCenterSoftPhoneWidgetDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAgentProfileManager agentProfileManager,
        IActivityQueueManager queueManager,
        ContactCenterAdminFormOptionsProvider optionsProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _agentProfileManager = agentProfileManager;
        _queueManager = queueManager;
        _optionsProvider = optionsProvider;
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
        var selectedCampaignIds = profile?.CampaignIds ?? [];
        var queues = await _queueManager.ListEnabledAsync();
        var viewModel = new AgentSoftPhoneViewModel
        {
            Profile = profile,
            AvailableQueues = [.. queues],
            SelectedQueueIds = profile?.QueueIds ?? [],
            CampaignOptions = await _optionsProvider.GetCampaignOptionsAsync(selectedCampaignIds),
            SelectedCampaignIds = selectedCampaignIds,
        };

        return Combine(
            View("ContactCenterSoftPhoneWork_Tab", viewModel)
                .Location("Detail", "Tabs:10"),
            View("ContactCenterSoftPhoneWork_View", viewModel)
                .Location("Detail", "Views:10")
        );
    }
}
