using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.Core.SignalR.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Serves the CRM-integrated agent desktop where an agent spends the shift: it renders the workspace
/// page, returns the live workspace state the real-time client binds to, changes the agent's presence,
/// and completes the active work through the source-neutral disposition path.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.RealTime)]
public sealed class AgentWorkspaceController : Controller
{
    private const int _recentHistoryCount = 10;

    private readonly IAuthorizationService _authorizationService;
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentPresenceManager _presenceManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly INamedCatalogManager<OmnichannelDisposition> _dispositionManager;
    private readonly IContentManager _contentManager;
    private readonly IActivityDispositionService _dispositionService;
    private readonly IAgentStateReasonCodeManager _reasonCodeManager;
    private readonly HubRouteManager _hubRouteManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentWorkspaceController"/> class.
    /// </summary>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="presenceManager">The agent presence manager.</param>
    /// <param name="reservationManager">The reservation manager used to resolve the pending offer.</param>
    /// <param name="queueManager">The queue manager used to resolve queue names.</param>
    /// <param name="queueItemManager">The queue item manager used to compute live queue depth.</param>
    /// <param name="interactionManager">The interaction manager used to resolve active and recent work.</param>
    /// <param name="activityManager">The CRM activity manager used to resolve activity context.</param>
    /// <param name="dispositionManager">The disposition catalog used to populate the wrap-up choices.</param>
    /// <param name="contentManager">The content manager used to resolve contact display names.</param>
    /// <param name="dispositionService">The source-neutral activity disposition service used to complete work.</param>
    /// <param name="reasonCodeManager">The agent state reason code manager used to build presence options.</param>
    /// <param name="hubRouteManager">The hub route manager used to resolve the real-time hub URL.</param>
    /// <param name="clock">The clock used to stamp times.</param>
    public AgentWorkspaceController(
        IAuthorizationService authorizationService,
        IAgentProfileManager agentManager,
        IAgentPresenceManager presenceManager,
        IActivityReservationManager reservationManager,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        INamedCatalogManager<OmnichannelDisposition> dispositionManager,
        IContentManager contentManager,
        IActivityDispositionService dispositionService,
        IAgentStateReasonCodeManager reasonCodeManager,
        HubRouteManager hubRouteManager,
        IClock clock)
    {
        _authorizationService = authorizationService;
        _agentManager = agentManager;
        _presenceManager = presenceManager;
        _reservationManager = reservationManager;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _dispositionManager = dispositionManager;
        _contentManager = contentManager;
        _dispositionService = dispositionService;
        _reasonCodeManager = reasonCodeManager;
        _hubRouteManager = hubRouteManager;
        _clock = clock;
    }

    /// <summary>
    /// Renders the agent desktop page.
    /// </summary>
    /// <returns>The agent workspace view.</returns>
    [Admin("contact-center/workspace", "ContactCenterAgentWorkspace")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var reasonCodes = await _reasonCodeManager.ListEnabledAsync();

        var viewModel = new AgentWorkspaceIndexViewModel
        {
            DisplayName = User.Identity?.Name,
            CanMonitor = await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.MonitorContactCenter),
            HubUrl = _hubRouteManager.GetPathByHub<ContactCenterHub>(),
            StateUrl = Url.Action(nameof(State)),
            SetPresenceUrl = Url.Action(nameof(SetPresence)),
            CompleteUrl = Url.Action(nameof(Complete)),
            AcceptOfferUrl = Url.RouteUrl("ContactCenterVoiceAcceptOffer"),
            DeclineOfferUrl = Url.RouteUrl("ContactCenterVoiceDeclineOffer"),
            SupervisorDashboardUrl = Url.Action(nameof(SupervisorDashboardController.Index), "SupervisorDashboard"),
            ReasonCodes = [.. reasonCodes.Select(code => new WorkspaceLookupViewModel
            {
                Id = code.AppliesTo.ToString(),
                Name = code.Name,
            })],
        };

        return View(viewModel);
    }

    /// <summary>
    /// Returns the live workspace state the agent desktop binds to.
    /// </summary>
    /// <returns>The agent workspace state.</returns>
    [HttpGet]
    [Admin("contact-center/workspace/state", "ContactCenterAgentWorkspaceState")]
    public async Task<IActionResult> State()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var now = _clock.UtcNow;

        var model = new AgentWorkspaceStateViewModel
        {
            UserId = userId,
            DisplayName = User.Identity?.Name,
            ServerTimeUtc = now,
        };

        var profile = await _agentManager.FindByUserIdAsync(userId, HttpContext.RequestAborted);

        if (profile is null)
        {
            return Json(model);
        }

        model.AgentId = profile.ItemId;
        model.HasProfile = true;
        model.DisplayName = string.IsNullOrEmpty(profile.DisplayName) ? model.DisplayName : profile.DisplayName;
        model.IsSignedIn = profile.QueueIds.Count > 0 || profile.CampaignIds.Count > 0;
        model.Presence = new WorkspacePresenceViewModel
        {
            Status = profile.PresenceStatus.ToString(),
            Reason = profile.PresenceReason,
            RequestedStatus = profile.RequestedPresenceStatus?.ToString(),
        };

        foreach (var queueId in profile.QueueIds)
        {
            var queue = await _queueManager.FindByIdAsync(queueId, HttpContext.RequestAborted);

            if (queue is null)
            {
                continue;
            }

            var waiting = await _queueItemManager.ListWaitingAsync(queueId, HttpContext.RequestAborted);

            model.Queues.Add(new WorkspaceQueueStatViewModel
            {
                Id = queueId,
                Name = queue.Name,
                WaitingCount = waiting.Count,
            });
        }

        model.Offer = await BuildOfferAsync(profile.ItemId, now, HttpContext.RequestAborted);
        model.ActiveInteraction = await BuildActiveInteractionAsync(profile.ItemId, HttpContext.RequestAborted);
        model.Dispositions = await BuildDispositionsAsync(HttpContext.RequestAborted);
        model.RecentHistory = await BuildRecentHistoryAsync(profile.ItemId, HttpContext.RequestAborted);

        return Json(model);
    }

    /// <summary>
    /// Changes the agent presence from the workspace presence control.
    /// </summary>
    /// <param name="status">The presence state to apply.</param>
    /// <param name="reason">The optional reason code.</param>
    /// <returns>An empty success result.</returns>
    [HttpPost]
    [Admin("contact-center/workspace/presence", "ContactCenterAgentWorkspacePresence")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPresence(AgentPresenceStatus status, string reason)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _presenceManager.SetPresenceAsync(userId, status, reason, HttpContext.RequestAborted);

        return Ok();
    }

    /// <summary>
    /// Completes the active activity with the selected disposition through the source-neutral disposition
    /// path so the configured subject flow runs and the activity is marked completed.
    /// </summary>
    /// <param name="activityId">The identifier of the activity to complete.</param>
    /// <param name="dispositionId">The selected disposition identifier.</param>
    /// <param name="notes">The optional wrap-up notes.</param>
    /// <returns>The completion result, or a problem result when it could not be applied.</returns>
    [HttpPost]
    [Admin("contact-center/workspace/complete", "ContactCenterAgentWorkspaceComplete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(string activityId, string dispositionId, string notes)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.SignIntoQueues))
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(activityId))
        {
            return BadRequest();
        }

        var activity = await _activityManager.FindByIdAsync(activityId, HttpContext.RequestAborted);

        if (activity is null)
        {
            return NotFound();
        }

        activity.DispositionId = dispositionId;

        var result = await _dispositionService.ApplyAsync(new ActivityDispositionRequest
        {
            Activity = activity,
            DispositionId = dispositionId,
            Notes = notes,
            Source = ActivityDispositionSource.Agent,
            ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ActorDisplayName = User.Identity?.Name,
        }, HttpContext.RequestAborted);

        return Json(new
        {
            result.Succeeded,
            result.ErrorMessage,
        });
    }

    private async Task<WorkspaceOfferViewModel> BuildOfferAsync(string agentId, DateTime now, CancellationToken cancellationToken)
    {
        var reservation = await _reservationManager.FindPendingByAgentAsync(agentId, cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);
        var queue = string.IsNullOrEmpty(reservation.QueueId)
            ? null
            : await _queueManager.FindByIdAsync(reservation.QueueId, cancellationToken);

        return new WorkspaceOfferViewModel
        {
            ReservationId = reservation.ItemId,
            ActivityItemId = reservation.ActivityItemId,
            QueueId = reservation.QueueId,
            QueueName = queue?.Name,
            CustomerLabel = await ResolveCustomerLabelAsync(activity, null, cancellationToken),
            CustomerAddress = activity?.PreferredDestination,
            ExpiresUtc = reservation.ExpiresUtc,
            ServerTimeUtc = now,
        };
    }

    private async Task<WorkspaceActiveInteractionViewModel> BuildActiveInteractionAsync(string agentId, CancellationToken cancellationToken)
    {
        var interaction = await _interactionManager.FindActiveByAgentAsync(agentId, cancellationToken);

        if (interaction is null)
        {
            return null;
        }

        var activity = string.IsNullOrEmpty(interaction.ActivityItemId)
            ? null
            : await _activityManager.FindByIdAsync(interaction.ActivityItemId, cancellationToken);

        var queue = string.IsNullOrEmpty(interaction.QueueId)
            ? null
            : await _queueManager.FindByIdAsync(interaction.QueueId, cancellationToken);

        return new WorkspaceActiveInteractionViewModel
        {
            InteractionId = interaction.ItemId,
            ActivityItemId = interaction.ActivityItemId,
            Direction = interaction.Direction.ToString(),
            Status = interaction.Status.ToString(),
            CustomerLabel = await ResolveCustomerLabelAsync(activity, interaction.CustomerAddress, cancellationToken),
            CustomerAddress = interaction.CustomerAddress,
            QueueName = queue?.Name,
            ContactUrl = BuildContactUrl(activity),
            StartedUtc = interaction.StartedUtc,
            AnsweredUtc = interaction.AnsweredUtc,
        };
    }

    private async Task<IList<WorkspaceLookupViewModel>> BuildDispositionsAsync(CancellationToken cancellationToken)
    {
        var page = await _dispositionManager.PageAsync(1, 200, new QueryContext(), cancellationToken);

        return [.. page.Entries.Select(disposition => new WorkspaceLookupViewModel
        {
            Id = disposition.ItemId,
            Name = disposition.Name,
        })];
    }

    private async Task<IList<WorkspaceHistoryEntryViewModel>> BuildRecentHistoryAsync(string agentId, CancellationToken cancellationToken)
    {
        var interactions = await _interactionManager.ListRecentByAgentAsync(agentId, _recentHistoryCount, cancellationToken);

        return [.. interactions.Select(interaction => new WorkspaceHistoryEntryViewModel
        {
            InteractionId = interaction.ItemId,
            Direction = interaction.Direction.ToString(),
            Status = interaction.Status.ToString(),
            CustomerLabel = interaction.CustomerAddress,
            CreatedUtc = interaction.CreatedUtc,
            EndedUtc = interaction.EndedUtc,
        })];
    }

    private async Task<string> ResolveCustomerLabelAsync(OmnichannelActivity activity, string fallback, CancellationToken cancellationToken)
    {
        if (activity is not null && !string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            var contact = await _contentManager.GetAsync(activity.ContactContentItemId, VersionOptions.Latest);

            if (contact is not null && !string.IsNullOrEmpty(contact.DisplayText))
            {
                return contact.DisplayText;
            }
        }

        return string.IsNullOrEmpty(fallback) ? activity?.PreferredDestination : fallback;
    }

    private string BuildContactUrl(OmnichannelActivity activity)
    {
        if (activity is null || string.IsNullOrEmpty(activity.ContactContentItemId))
        {
            return null;
        }

        return Url.Action("Edit", "Admin", new { area = "OrchardCore.Contents", contentItemId = activity.ContactContentItemId });
    }
}
