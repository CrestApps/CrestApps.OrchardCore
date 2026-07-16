using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Contributes Contact Center context to the soft-phone incoming-call modal. For a ringing inbound
/// call offered to an agent, it lists the customers matched by the caller's phone number (scoped by
/// the agent's signed-in inbound queue) and wires the accept and decline offer-lifecycle actions.
/// </summary>
public sealed class ContactCenterIncomingCallContextProvider : IIncomingCallContextProvider
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IInboundContactLookup _contactLookup;
    private readonly IContentManager _contentManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterIncomingCallContextProvider"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="reservationManager">The reservation manager used to resolve the agent's pending offer.</param>
    /// <param name="queueManager">The queue manager used to resolve the offered queue name.</param>
    /// <param name="contactLookup">The contact lookup used to match customers by phone number.</param>
    /// <param name="contentManager">The content manager used to load matched contact content items.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to build same-origin URLs.</param>
    /// <param name="linkGenerator">The link generator used to build the contact and offer-lifecycle URLs.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterIncomingCallContextProvider(
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IActivityQueueManager queueManager,
        IInboundContactLookup contactLookup,
        IContentManager contentManager,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IStringLocalizer<ContactCenterIncomingCallContextProvider> stringLocalizer)
    {
        _agentManager = agentManager;
        _reservationManager = reservationManager;
        _queueManager = queueManager;
        _contactLookup = contactLookup;
        _contentManager = contentManager;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task ContributeAsync(IncomingCallContributionContext context, CancellationToken cancellationToken = default)
    {
        var call = context.Call;

        if (call is null || call.Direction != CallDirection.Inbound || string.IsNullOrEmpty(context.UserId))
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var queueName = await ContributeOfferLifecycleAsync(context, httpContext, cancellationToken);

        if (string.IsNullOrEmpty(call.From))
        {
            return;
        }

        var contactIds = await _contactLookup.FindContactItemIdsAsync(call.From, cancellationToken);

        if (contactIds.Count == 0)
        {
            return;
        }

        var contacts = await _contentManager.GetAsync(contactIds, VersionOptions.Latest);
        context.Heading = S["Matched customers"];

        var priority = 0;

        foreach (var contact in contacts)
        {
            var card = new IncomingCallCard
            {
                Id = contact.ContentItemId,
                Title = string.IsNullOrEmpty(contact.DisplayText) ? call.From : contact.DisplayText,
                Subtitle = call.From,
                Icon = "fa-solid fa-user",
                Source = ContactCenterConstants.Components.Voice,
                Priority = priority++,
            };

            if (!string.IsNullOrEmpty(queueName))
            {
                card.Badges.Add(queueName);
            }

            if (httpContext is not null)
            {
                card.Url = _linkGenerator.GetPathByAction(
                    httpContext,
                    action: "Edit",
                    controller: "Admin",
                    values: new { area = "OrchardCore.Contents", contentItemId = contact.ContentItemId });
            }

            context.Cards.Add(card);
        }
    }

    private async Task<string> ContributeOfferLifecycleAsync(IncomingCallContributionContext context, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var agent = await _agentManager.FindByUserIdAsync(context.UserId, cancellationToken);

        if (agent is null)
        {
            return null;
        }

        var reservation = await _reservationManager.FindPendingByAgentAsync(agent.ItemId, cancellationToken);

        if (reservation is null)
        {
            return null;
        }

        if (httpContext is not null)
        {
            var acceptUrl = _linkGenerator.GetPathByName(httpContext, "ContactCenterVoiceAcceptOffer", new { reservationId = reservation.ItemId });
            var declineUrl = _linkGenerator.GetPathByName(httpContext, "ContactCenterVoiceDeclineOffer", new { reservationId = reservation.ItemId });

            if (!string.IsNullOrEmpty(acceptUrl))
            {
                context.Properties["acceptUrl"] = acceptUrl;
            }

            if (!string.IsNullOrEmpty(declineUrl))
            {
                context.Properties["declineUrl"] = declineUrl;
            }
        }

        context.Properties["reservationId"] = reservation.ItemId;
        context.Properties["expiresUtc"] = reservation.ExpiresUtc.ToString("O");

        context.Call.Metadata ??= new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        context.Call.Metadata["voicemailRecipientUserId"] = agent.UserId;

        if (!string.IsNullOrWhiteSpace(agent.UserName))
        {
            context.Call.Metadata["voicemailRecipientUserName"] = agent.UserName;
        }

        if (!string.IsNullOrWhiteSpace(agent.DisplayName))
        {
            context.Call.Metadata["voicemailRecipientDisplayName"] = agent.DisplayName;
        }

        if (string.IsNullOrEmpty(reservation.QueueId))
        {
            return null;
        }

        var queue = await _queueManager.FindByIdAsync(reservation.QueueId, cancellationToken);

        if (queue is null)
        {
            return null;
        }

        context.Properties["queue"] = queue.Name;
        context.Call.Metadata["queueId"] = queue.ItemId;
        context.Call.Metadata["queueName"] = queue.Name;

        return queue.Name;
    }
}
