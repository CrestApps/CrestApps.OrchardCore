using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

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
    private readonly IAgentPreDialLegStore _preDialLegStore;
    private readonly IActivityQueueManager _queueManager;
    private readonly IInboundContactLookup _contactLookup;
    private readonly IContentManager _contentManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly ShellSettings _shellSettings;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterIncomingCallContextProvider"/> class.
    /// </summary>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="reservationManager">The reservation manager used to resolve the agent's pending offer.</param>
    /// <param name="preDialLegStore">The store of agent legs rung while their offers were still ringing.</param>
    /// <param name="queueManager">The queue manager used to resolve the offered queue name.</param>
    /// <param name="contactLookup">The contact lookup used to match customers by phone number.</param>
    /// <param name="contentManager">The content manager used to load matched contact content items.</param>
    /// <param name="activityManager">The activity manager used to resolve the customer the offered activity belongs to.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the path base of a live request.</param>
    /// <param name="linkGenerator">The link generator used to build the contact and offer-lifecycle URLs.</param>
    /// <param name="shellSettings">The tenant settings whose URL prefix is the path base when no request is live.</param>
    /// <param name="clock">The clock the offer's deadline is measured against, sent so a client can correct for its own.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterIncomingCallContextProvider(
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IAgentPreDialLegStore preDialLegStore,
        IActivityQueueManager queueManager,
        IInboundContactLookup contactLookup,
        IContentManager contentManager,
        IOmnichannelActivityManager activityManager,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        ShellSettings shellSettings,
        IClock clock,
        IStringLocalizer<ContactCenterIncomingCallContextProvider> stringLocalizer)
    {
        _agentManager = agentManager;
        _reservationManager = reservationManager;
        _preDialLegStore = preDialLegStore;
        _queueManager = queueManager;
        _contactLookup = contactLookup;
        _contentManager = contentManager;
        _activityManager = activityManager;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _shellSettings = shellSettings;
        _clock = clock;
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

        // Every URL is built from the path base alone, never from the request itself. An offer is often dispatched
        // from work that outlives the webhook request that started it (an AI handoff runs off the request thread),
        // and the accessor can still hand back that request after it has been disposed: link generation reads its
        // features and throws, and the offer went out without its accept and decline actions.
        var pathBase = ResolvePathBase();
        var agent = await _agentManager.FindByUserIdAsync(context.UserId, cancellationToken);
        var reservation = agent is null
            ? null
            : await _reservationManager.FindPendingByAgentAsync(agent.ItemId, cancellationToken);
        var queueName = await ContributeOfferLifecycleAsync(context, agent, reservation, pathBase, cancellationToken);

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
        var offeredActivityContactId = await FindOfferedActivityContactIdAsync(reservation, cancellationToken);
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

            var contactUrl = _linkGenerator.GetPathByAction(
                action: "Edit",
                controller: "Admin",
                values: new { area = "OrchardCore.Contents", contentItemId = contact.ContentItemId },
                pathBase: pathBase);

            card.Url = contactUrl;

            // The customer the offered activity belongs to opens that activity's completion screen -- the notes and
            // the disposition for this call -- rather than the customer's edit screen, where the agent had to find
            // the activity first. The record stays one click away, and completing returns to it.
            if (string.Equals(contact.ContentItemId, offeredActivityContactId, StringComparison.Ordinal))
            {
                var activityUrl = _linkGenerator.GetPathByAction(
                    action: "Complete",
                    controller: "Activities",
                    values: new { area = OmnichannelConstants.Features.Managements, id = reservation.ActivityItemId, returnUrl = contactUrl },
                    pathBase: pathBase);

                if (!string.IsNullOrEmpty(activityUrl))
                {
                    card.Url = activityUrl;
                    card.OpenText = S["Open activity"];
                    card.AnswerAndOpenText = S["Answer & open activity"];
                    card.Links.Add(new IncomingCallCardLink
                    {
                        Text = S["Customer record"],
                        Url = contactUrl,
                        Icon = "fa-solid fa-address-card",
                    });
                }
            }

            context.Cards.Add(card);
        }
    }

    private async Task<string> FindOfferedActivityContactIdAsync(ActivityReservation reservation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reservation?.ActivityItemId))
        {
            return null;
        }

        var activity = await _activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

        return activity?.ContactContentItemId;
    }

    private async Task<string> ContributeOfferLifecycleAsync(
        IncomingCallContributionContext context,
        AgentProfile agent,
        ActivityReservation reservation,
        PathString pathBase,
        CancellationToken cancellationToken)
    {
        if (agent is null || reservation is null)
        {
            return null;
        }

        // The offer accept/decline actions must be present even when there is no ambient request. The queue
        // ring is dispatched from the reservation event on a background scope (no HttpContext), and that push
        // reaches the soft-phone page too; if its context omitted these actions it would downgrade the modal
        // and the page could no longer accept the reservation.
        var acceptUrl = _linkGenerator.GetPathByName("ContactCenterVoiceAcceptOffer", new { reservationId = reservation.ItemId }, pathBase);
        var declineUrl = _linkGenerator.GetPathByName("ContactCenterVoiceDeclineOffer", new { reservationId = reservation.ItemId }, pathBase);

        if (!string.IsNullOrEmpty(acceptUrl))
        {
            context.Properties["acceptUrl"] = acceptUrl;
        }

        if (!string.IsNullOrEmpty(declineUrl))
        {
            context.Properties["declineUrl"] = declineUrl;
        }

        // The offer's own route to voicemail. A Contact Center offer is sent to voicemail by the Contact Center alone,
        // so the soft phone never also asks the telephony hub and the caller is answered and greeted once.
        var voicemailUrl = _linkGenerator.GetPathByName("ContactCenterVoiceSendOfferToVoicemail", new { reservationId = reservation.ItemId }, pathBase);

        if (!string.IsNullOrEmpty(voicemailUrl))
        {
            context.Properties["voicemailUrl"] = voicemailUrl;
        }

        context.Properties["reservationId"] = reservation.ItemId;
        context.Properties["expiresUtc"] = reservation.ExpiresUtc.ToString("O");

        // The deadline is the server's, so the server's clock travels with it: a phone whose own clock has drifted
        // still stops ringing at the moment the server moves the caller on, not before.
        context.Properties["serverTimeUtc"] = _clock.UtcNow.ToString("O");

        // The leg already rung to the agent's device for this offer, when there is one. The soft phone recognizes that
        // leg by the offer id it carries; this is the provider's own id for it, for a client that sees nothing else.
        var preDialedLeg = await _preDialLegStore.FindAsync(reservation.ItemId, cancellationToken);

        if (!string.IsNullOrEmpty(preDialedLeg?.AgentLegId) && !preDialedLeg.BridgedUtc.HasValue)
        {
            context.Properties["agentLegId"] = preDialedLeg.AgentLegId;
        }

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

    /// <summary>
    /// The path base the same-origin URLs are built under: the live request's, which carries a hosted path base as
    /// well as the tenant prefix, or the tenant prefix alone when no request is live.
    /// </summary>
    private PathString ResolvePathBase()
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext is not null)
            {
                return httpContext.Request.PathBase;
            }
        }
        catch (ObjectDisposedException)
        {
            // The request this work started on has ended; the tenant prefix below is all that is left of it.
        }

        return string.IsNullOrEmpty(_shellSettings.RequestUrlPrefix)
            ? PathString.Empty
            : new PathString("/" + _shellSettings.RequestUrlPrefix.Trim('/'));
    }
}
