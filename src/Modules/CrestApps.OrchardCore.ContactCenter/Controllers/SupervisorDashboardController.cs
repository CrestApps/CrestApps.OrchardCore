using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Serves the supervisor dashboard: a live wallboard of queue depth, service-level health, and agent
/// presence that a contact center manager uses to monitor operations in real time.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.RealTime)]
public sealed class SupervisorDashboardController : Controller
{
    private const int _maxAgents = 200;

    private readonly IAuthorizationService _authorizationService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterMonitoringService _monitoringService;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly HubRouteManager _hubRouteManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SupervisorDashboardController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="queueManager">The queue manager used to enumerate queues.</param>
    /// <param name="queueItemManager">The queue item manager used to compute live queue depth and SLA health.</param>
    /// <param name="agentManager">The agent profile manager used to build the agent board.</param>
    /// <param name="interactionManager">The interaction manager used to count active work per agent.</param>
    /// <param name="monitoringServices">The optional services used to start audited supervisor live-monitoring engagements.</param>
    /// <param name="userManager">The user manager used to resolve Orchard users.</param>
    /// <param name="displayNameProvider">The display name provider used to render agent full names.</param>
    /// <param name="hubRouteManager">The hub route manager used to resolve the real-time hub URL.</param>
    /// <param name="clock">The clock used to compute wait times.</param>
    public SupervisorDashboardController(
        IAuthorizationService authorizationService,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        IEnumerable<IContactCenterMonitoringService> monitoringServices,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        HubRouteManager hubRouteManager,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _monitoringService = monitoringServices.FirstOrDefault();
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _hubRouteManager = hubRouteManager;
        _clock = clock;
    }

    /// <summary>
    /// Renders the supervisor dashboard page.
    /// </summary>
    /// <returns>The supervisor dashboard view.</returns>
    [Admin("contact-center/dashboard", "ContactCenterSupervisorDashboard")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.MonitorContactCenter))
        {
            return Forbid();
        }

        var viewModel = new SupervisorDashboardIndexViewModel
        {
            HubUrl = _hubRouteManager.GetPathByHub<ContactCenterHub>(),
            StateUrl = Url.Action(nameof(State)),
            EngageUrl = Url.Action(nameof(Engage)),
        };

        return View(viewModel);
    }

    /// <summary>
    /// Returns the live supervisor dashboard state.
    /// </summary>
    /// <returns>The supervisor dashboard state.</returns>
    [HttpGet]
    [Admin("contact-center/dashboard/state", "ContactCenterSupervisorDashboardState")]
    public async Task<IActionResult> State()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.MonitorContactCenter))
        {
            return Forbid();
        }

        var now = _clock.UtcNow;
        var model = new SupervisorDashboardStateViewModel
        {
            ServerTimeUtc = now,
        };

        var queues = await _queueManager.ListEnabledAsync(HttpContext.RequestAborted);

        foreach (var queue in queues)
        {
            var waiting = await _queueItemManager.ListWaitingAsync(queue.ItemId, HttpContext.RequestAborted);
            var longestWaitSeconds = 0;
            var slaBreachCount = 0;

            foreach (var item in waiting)
            {
                var waitSeconds = (int)Math.Max(0, (now - item.EnqueuedUtc).TotalSeconds);
                longestWaitSeconds = Math.Max(longestWaitSeconds, waitSeconds);

                if (queue.SlaThresholdSeconds > 0 && waitSeconds > queue.SlaThresholdSeconds)
                {
                    slaBreachCount++;
                }
            }

            model.Queues.Add(new SupervisorQueueViewModel
            {
                Id = queue.ItemId,
                Name = queue.Name,
                WaitingCount = waiting.Count,
                LongestWaitSeconds = longestWaitSeconds,
                SlaBreachCount = slaBreachCount,
                SlaThresholdSeconds = queue.SlaThresholdSeconds,
            });

            model.TotalWaiting += waiting.Count;
        }

        var agents = await _agentManager.PageAsync(1, _maxAgents, new QueryContext(), HttpContext.RequestAborted);

        foreach (var agent in agents.Entries)
        {
            var activeInteractions = await _interactionManager.CountActiveByAgentAsync(agent.ItemId, HttpContext.RequestAborted);
            var activeInteraction = await _interactionManager.FindActiveByAgentAsync(agent.ItemId, HttpContext.RequestAborted);

            model.Agents.Add(new SupervisorAgentViewModel
            {
                AgentId = agent.ItemId,
                UserId = agent.UserId,
                DisplayName = await GetAgentDisplayNameAsync(agent),
                PresenceStatus = agent.PresenceStatus.ToString(),
                PresenceReason = agent.PresenceReason,
                QueueCount = agent.QueueIds.Count,
                ActiveInteractions = activeInteractions,
                ActiveInteractionId = activeInteraction?.ItemId,
            });

            if (agent.PresenceStatus == AgentPresenceStatus.Available)
            {
                model.AvailableAgents++;
            }
        }

        return Json(model);
    }

    /// <summary>
    /// Starts an audited supervisor live-monitoring engagement for the selected interaction.
    /// </summary>
    /// <param name="interactionId">The live interaction identifier.</param>
    /// <param name="mode">The supervisor engagement mode.</param>
    /// <returns>The engagement result.</returns>
    [HttpPost]
    [Admin("contact-center/dashboard/engage", "ContactCenterSupervisorDashboardEngage")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Engage(string interactionId, MonitorMode mode)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.MonitorContactCenter))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(interactionId))
        {
            return BadRequest();
        }

        if (_monitoringService is null)
        {
            return BadRequest();
        }

        var supervisorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(supervisorId))
        {
            return Forbid();
        }

        var result = await _monitoringService.EngageAsync(interactionId, supervisorId, mode, HttpContext.RequestAborted);

        return Json(new
        {
            result.Succeeded,
            ErrorMessage = result.Reason,
        });
    }

    private async Task<string> GetAgentDisplayNameAsync(AgentProfile agent)
    {
        if (!string.IsNullOrEmpty(agent.UserId))
        {
            var user = await _userManager.FindByIdAsync(agent.UserId);

            if (user is not null)
            {
                var displayName = await _displayNameProvider.GetAsync(user, HttpContext.RequestAborted);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return string.IsNullOrEmpty(agent.DisplayName) ? agent.UserName : agent.DisplayName;
    }
}
