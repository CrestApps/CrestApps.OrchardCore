using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class DefaultSubjectActionExecutor : ISubjectActionExecutor
{
    private readonly ISourceCatalog<SubjectAction> _actionCatalog;
    private readonly ISubjectFlowSettingsService _subjectFlowSettingsService;
    private readonly IContentManager _contentManager;
    private readonly INotInServiceNumberService _notInServiceNumbers;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly IContactMethodsAccessor _contactMethods;
    private readonly ILogger _logger;
    private readonly IEnumerable<ISubjectActionHandler> _handlers;
    private readonly IEnumerable<IFollowUpActivityHandler> _followUpHandlers;
    private readonly INamedCatalog<LeadStatus> _leadStatuses;
    private readonly IActivityDialerContributor _dialerContributor;
    private readonly IActivityCallAgentResolver _callAgentResolver;
    private readonly FollowUpOwnerPicker _ownerPicker;

    public DefaultSubjectActionExecutor(
        ISourceCatalog<SubjectAction> actionCatalog,
        ISubjectFlowSettingsService subjectFlowSettingsService,
        IContentManager contentManager,
        INotInServiceNumberService notInServiceNumbers,
        ISession session,
        IClock clock,
        ILocalClock localClock,
        IContactMethodsAccessor contactMethods,
        ILogger<DefaultSubjectActionExecutor> logger,
        IEnumerable<ISubjectActionHandler> handlers = null,
        IEnumerable<IFollowUpActivityHandler> followUpHandlers = null,
        INamedCatalog<LeadStatus> leadStatuses = null,
        IEnumerable<IActivityDialerContributor> dialerContributors = null,
        IEnumerable<IActivityCallAgentResolver> callAgentResolvers = null,
        IEnumerable<FollowUpOwnerPicker> ownerPickers = null)
    {
        _actionCatalog = actionCatalog;
        _subjectFlowSettingsService = subjectFlowSettingsService;
        _contentManager = contentManager;
        _notInServiceNumbers = notInServiceNumbers;
        _session = session;
        _clock = clock;
        _localClock = localClock;
        _contactMethods = contactMethods;
        _logger = logger;
        _handlers = handlers ?? [];
        _followUpHandlers = followUpHandlers ?? [];
        _leadStatuses = leadStatuses;
        _dialerContributor = dialerContributors?.FirstOrDefault();
        _callAgentResolver = callAgentResolvers?.FirstOrDefault();
        _ownerPicker = ownerPickers?.FirstOrDefault();
    }

    public async Task ExecuteAsync(SubjectActionExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Activity);
        ArgumentNullException.ThrowIfNull(context.Disposition);

        if (context.Activity.ContactResolutionStatus is ContactResolutionStatus.Unresolved or ContactResolutionStatus.Ambiguous ||
            context.Activity.ContactResolutionStatus == ContactResolutionStatus.Unknown &&
            string.Equals(context.Activity.Source, ActivitySources.Inbound, StringComparison.Ordinal))
        {
            // An agent resolves the contact when completing their own activity, and the actions run then. Nobody resolves
            // the contact on an automated activity once the AI has concluded it, so a new activity -- which needs no
            // contact, and carries the number and the resolution to its owner -- still runs. The rest needs the contact.
            if (context.Activity.InteractionType == ActivityInteractionType.Automated)
            {
                await ExecuteNewActivitiesWithoutContactAsync(context, cancellationToken);

                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Deferred subject actions for activity {ActivityId} because contact resolution is {ContactResolutionStatus}.",
                    context.Activity.ItemId,
                    context.Activity.ContactResolutionStatus);
            }

            return;
        }

        // What the disposition itself means is applied once, whatever actions the subject wires to it -- and when it
        // wires none, which is how a disposition the platform chose on its own is usually recorded.
        await ApplyOutcomeAsync(context);

        var allActions = await _actionCatalog.GetAllAsync(cancellationToken);

        var actions = allActions
            .Where(a => string.Equals(a.SubjectContentType, context.Activity.SubjectContentType, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.DispositionId, context.Disposition.ItemId, StringComparison.OrdinalIgnoreCase))
            // An action that changes which record the activity is about, such as converting a lead, runs first so
            // the actions after it work on the new record. The sort is stable, so the others keep their order.
            .OrderBy(a => GetHandler(a.Source)?.Order ?? 0)
            .ToArray();

        foreach (var action in actions)
        {
            await ExecuteActionAsync(action, context);
        }

        await ApplyDispositionLeadStatusAsync(context, actions);

        if (!NamesLeadStatus(context.Disposition) && !actions.Any(NamesLeadStatus))
        {
            await ApplyFallbackLeadStatusAsync(context, cancellationToken);
        }
    }

    private static bool NamesLeadStatus(OmnichannelDisposition disposition)
        => disposition.TryGet<SetLeadStatusActionMetadata>(out var metadata) && !string.IsNullOrEmpty(metadata.StatusId);

    private static bool NamesLeadStatus(SubjectAction action)
        => action.TryGet<SetLeadStatusActionMetadata>(out var metadata) && !string.IsNullOrEmpty(metadata.StatusId);

    /// <summary>
    /// Moves a lead whose disposition and actions name no status of their own. A lead whose every number is now known not
    /// to be in service cannot be reached, so it moves to the first closed status; a lead still in the default status
    /// was just contacted, or tried, so it moves to the contacted status. Without this, a lead called once stayed
    /// <c>Open - Not Contacted</c> and a load that picks that status loaded it again.
    /// </summary>
    private async Task ApplyFallbackLeadStatusAsync(SubjectActionExecutionContext context, CancellationToken cancellationToken)
    {
        var contact = context.Contact;

        if (_leadStatuses is null ||
            contact is null ||
            !contact.TryGet<LeadPart>(out var leadPart) ||
            leadPart.IsConverted)
        {
            return;
        }

        var statuses = await _leadStatuses.GetAllAsync(cancellationToken);
        var current = statuses.FirstOrDefault(status => status.ItemId == leadPart.StatusId);

        if (current is { IsClosed: true })
        {
            return;
        }

        if (context.Disposition.Outcome == DispositionOutcome.NotInService &&
            await HasNoNumberInServiceAsync(context, contact, cancellationToken))
        {
            var closed = statuses
                .Where(status => status.IsClosed && !status.IsConverted)
                .OrderBy(status => status.Order)
                .FirstOrDefault();

            if (closed is not null)
            {
                await ApplyLeadStatusAsync(closed.ItemId, contact);

                return;
            }
        }

        // A lead with no status yet is treated as being in the default one, which is where a new lead starts.
        if (current is not null && !current.IsDefault)
        {
            return;
        }

        var contacted = statuses.FirstOrDefault(status => status.IsContacted && !status.IsClosed && !status.IsConverted);

        if (contacted is not null)
        {
            await ApplyLeadStatusAsync(contacted.ItemId, contact);
        }
    }

    private async Task<bool> HasNoNumberInServiceAsync(SubjectActionExecutionContext context, ContentItem contact, CancellationToken cancellationToken)
    {
        var numbers = OmnichannelHelper.GetPhoneNumbers(_contactMethods.Get(contact))
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (numbers.Length == 0)
        {
            return true;
        }

        // The number this activity dialed was just marked, and the mark may not be readable until it is committed.
        var dialed = context.Activity.PreferredDestination;
        var notInService = await _notInServiceNumbers.GetNotInServiceAsync(numbers, cancellationToken);

        return numbers.All(number => notInService.Contains(number) ||
            (!string.IsNullOrWhiteSpace(dialed) && string.Equals(number, dialed, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Moves the lead to the status the disposition itself names, so a disposition applies in every subject -- including
    /// one the platform picked on its own, such as a number not in service, which no subject wires actions to. A subject
    /// action for the disposition that names a status of its own takes precedence.
    /// </summary>
    private Task ApplyDispositionLeadStatusAsync(SubjectActionExecutionContext context, SubjectAction[] actions)
    {
        if (!context.Disposition.TryGet<SetLeadStatusActionMetadata>(out var metadata) ||
            string.IsNullOrEmpty(metadata.StatusId) ||
            actions.Any(action => action.TryGet<SetLeadStatusActionMetadata>(out var actionMetadata) && !string.IsNullOrEmpty(actionMetadata.StatusId)))
        {
            return Task.CompletedTask;
        }

        return ApplyLeadStatusAsync(metadata.StatusId, context.Contact);
    }

    private async Task ExecuteNewActivitiesWithoutContactAsync(SubjectActionExecutionContext context, CancellationToken cancellationToken)
    {
        var allActions = await _actionCatalog.GetAllAsync(cancellationToken);

        var actions = allActions
            .Where(a => string.Equals(a.SubjectContentType, context.Activity.SubjectContentType, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.DispositionId, context.Disposition.ItemId, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.Source, OmnichannelConstants.ActionTypes.NewActivity, StringComparison.Ordinal))
            .ToArray();

        foreach (var action in actions)
        {
            await ExecuteNewActivityAsync(action, context);
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Ran {Count} new-activity action(s) for automated activity {ActivityId} and deferred the rest, because contact resolution is {ContactResolutionStatus}.",
                actions.Length,
                context.Activity.ItemId,
                context.Activity.ContactResolutionStatus);
        }
    }

    private async Task ExecuteActionAsync(SubjectAction action, SubjectActionExecutionContext context)
    {
        await ApplyCommunicationPreferencesAsync(action, context.Contact);
        await ApplyLeadStatusAsync(action, context.Contact);

        switch (action.Source)
        {
            case OmnichannelConstants.ActionTypes.Finish:
                break;

            case OmnichannelConstants.ActionTypes.TryAgain:
                await ExecuteTryAgainAsync(action, context);
                break;

            case OmnichannelConstants.ActionTypes.NewActivity:
                await ExecuteNewActivityAsync(action, context);
                break;

            default:
                var handler = GetHandler(action.Source);

                if (handler is null)
                {
                    _logger.LogWarning("Unknown subject action type: {ActionType}", action.Source);
                    break;
                }

                await handler.ExecuteAsync(action, context);
                break;
        }
    }

    private async Task ExecuteTryAgainAsync(SubjectAction action, SubjectActionExecutionContext context)
    {
        var activity = context.Activity;

        if (!action.TryGet<TryAgainActionMetadata>(out var metadata))
        {
            metadata = new TryAgainActionMetadata();
        }

        if (metadata.MaxAttempt.HasValue && activity.Attempts >= metadata.MaxAttempt.Value)
        {
            return;
        }

        var now = _clock.UtcNow;
        var nextAttempt = OmnichannelActivityFollowUps.CreateNextAttempt(activity, now);
        nextAttempt.Instructions = ResolvePreparationNotes(action, context, activity.Instructions);
        nextAttempt.UrgencyLevel = metadata.UrgencyLevel ?? activity.UrgencyLevel;
        nextAttempt.ScheduledUtc = await ResolveScheduleDateAsync(action, context, metadata.DefaultScheduleHours);

        // A maximum named on the action is the subject's own limit for this contact, and it stands in for any limit the
        // work's carrier has: a dialer campaign record may be tried as often as the subject says.
        nextAttempt.MaxAttempts = metadata.MaxAttempt;

        if (!await TryAssignOwnerAsync(
            nextAttempt,
            action,
            metadata.AssignmentType,
            metadata.NormalizedUserName,
            activity,
            now))
        {
            return;
        }

        var route = await ApplyRoutingAsync(action, nextAttempt);
        await FillMissingOwnerAsync(nextAttempt, action, metadata.AssignmentType, metadata.NormalizedUserName, activity, now);

        // Whatever carried the first attempt takes the next one in too -- a dialer campaign queues it again -- or
        // refuses it when no attempt is left. A follow-up the action gave to an agent stays off the dialer, and one it
        // sent to the dialer is queued on its own campaign under its own profile.
        var followUp = new FollowUpActivityContext
        {
            Route = route,
            PreviousActivity = activity,
            FollowUpActivity = nextAttempt,
            CreatedBy = OmnichannelConstants.ActionTypes.TryAgain,
            HasNamedOwner = SubjectActionOwnerAssignmentTypeResolver.Resolve(metadata.AssignmentType, metadata.NormalizedUserName) != SubjectActionOwnerAssignmentType.SameOwner,
            MaxAttempts = metadata.MaxAttempt,
            HasChosenSchedule = HasChosenScheduleDate(action, context) || metadata.DefaultScheduleHours.HasValue,
        };

        foreach (var handler in _followUpHandlers)
        {
            await handler.CreatingAsync(followUp);
        }

        if (followUp.Cancel)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Did not create the next attempt of activity {ActivityId}: {Reason}",
                    activity.ItemId,
                    followUp.CancelReason);
            }

            return;
        }

        await _session.SaveAsync(nextAttempt, collection: OmnichannelConstants.CollectionName);
        context.CreatedActivities[action.ItemId] = nextAttempt;
    }

    private async Task ExecuteNewActivityAsync(SubjectAction action, SubjectActionExecutionContext context)
    {
        var activity = context.Activity;

        if (!action.TryGet<NewActivityActionMetadata>(out var metadata))
        {
            metadata = new NewActivityActionMetadata();
        }

        var targetSubjectContentType = !string.IsNullOrEmpty(metadata.SubjectContentType)
            ? metadata.SubjectContentType
            : activity.SubjectContentType;

        var flowSettings = await FindFlowSettingsForSubjectAsync(targetSubjectContentType);

        var now = _clock.UtcNow;
        var newActivity = new OmnichannelActivity
        {
            ItemId = IdGenerator.GenerateId(),
            Channel = activity.Channel,
            ChannelEndpointId = activity.ChannelEndpointId,
            InteractionType = activity.InteractionType,
            PreferredDestination = activity.PreferredDestination,
            ContactContentItemId = activity.ContactContentItemId,
            ContactContentType = activity.ContactContentType,
            ContactResolutionStatus = activity.ContactResolutionStatus,
            ContactResolutionCandidates = activity.ContactResolutionCandidates.ToList(),
            ContactResolvedUtc = activity.ContactResolvedUtc,
            ContactResolvedById = activity.ContactResolvedById,
            ContactResolvedByUsername = activity.ContactResolvedByUsername,
            CampaignId = activity.CampaignId,
            // An agent writes a follow-up's notes on the Complete page. Nobody does for a call the AI concluded, so its summary of the
            // call is what the follow-up's owner reads -- the message, when the caller left one.
            Instructions = ResolvePreparationNotes(
                action,
                context,
                activity.InteractionType == ActivityInteractionType.Automated && !string.IsNullOrWhiteSpace(activity.Notes) ? activity.Notes.Trim() : null),
            Attempts = 1,
            CreatedById = activity.CompletedById,
            CreatedByUsername = activity.CompletedByUsername,
            CreatedUtc = now,
            SubjectContentType = targetSubjectContentType,
            UrgencyLevel = metadata.UrgencyLevel ?? activity.UrgencyLevel,
            Status = ActivityStatus.NotStated,
        };

        newActivity.ScheduledUtc = await ResolveScheduleDateAsync(action, context, metadata.DefaultScheduleHours);

        if (!await TryAssignOwnerAsync(
            newActivity,
            action,
            metadata.AssignmentType,
            metadata.NormalizedUserName,
            activity,
            now))
        {
            return;
        }

        newActivity.Subject = await _contentManager.NewAsync(targetSubjectContentType);

        if (flowSettings != null)
        {
            newActivity.Channel = flowSettings.Channel ?? activity.Channel;
            newActivity.InteractionType = flowSettings.InteractionType;
            newActivity.ChannelEndpointId = flowSettings.ChannelEndpointId ?? activity.ChannelEndpointId;
            newActivity.CampaignId = flowSettings.CampaignId ?? activity.CampaignId;
        }

        if (context.Contact is not null)
        {
            newActivity.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(context.Contact, _contactMethods.Get(context.Contact), newActivity.Channel);
        }

        // A new activity the action sends to the dialer is queued on its campaign by the same handlers that queue a
        // dialer retry, so it is dialed under the profile the action names.
        var newActivityRoute = await ApplyRoutingAsync(action, newActivity);
        await FillMissingOwnerAsync(newActivity, action, metadata.AssignmentType, metadata.NormalizedUserName, activity, now);

        if (newActivityRoute == FollowUpRoutingTarget.Dialer)
        {
            var followUp = new FollowUpActivityContext
            {
                PreviousActivity = activity,
                FollowUpActivity = newActivity,
                CreatedBy = OmnichannelConstants.ActionTypes.NewActivity,
                Route = FollowUpRoutingTarget.Dialer,
                HasChosenSchedule = true,
            };

            foreach (var handler in _followUpHandlers)
            {
                await handler.CreatingAsync(followUp);
            }

            if (followUp.Cancel)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "Did not create the new activity of activity {ActivityId} for the dialer: {Reason}",
                        activity.ItemId,
                        followUp.CancelReason);
                }

                return;
            }
        }

        await _session.SaveAsync(newActivity, collection: OmnichannelConstants.CollectionName);
        context.CreatedActivities[action.ItemId] = newActivity;
    }

    /// <summary>
    /// Routes the follow-up the action created the way the action says: to an agent (off the dialer), to the dialer
    /// under a profile, or only to another campaign.
    /// </summary>
    /// <remarks>
    /// A call back the dialer placed used to go back to the dialer whatever the subject flow wanted: "Try again" queued
    /// the next attempt in the same campaign under the same profile. The action can now give the call to an agent, so
    /// the dialer never calls it again, send manual work to the dialer, or move it to another campaign.
    /// </remarks>
    private async Task<FollowUpRoutingTarget> ApplyRoutingAsync(SubjectAction action, OmnichannelActivity followUp)
    {
        if (!action.TryGet<FollowUpRoutingMetadata>(out var routing) || !routing.ChangesRouting())
        {
            return FollowUpRoutingTarget.SameAsBefore;
        }

        var campaignId = string.IsNullOrWhiteSpace(routing.CampaignId) ? followUp.CampaignId : routing.CampaignId.Trim();

        switch (routing.Target)
        {
            case FollowUpRoutingTarget.Agent:
                var source = string.IsNullOrWhiteSpace(routing.Source) || !FollowUpRoutes.IsAgentSource(routing.Source)
                    ? ActivitySources.Manual
                    : routing.Source.Trim();

                FollowUpRoutes.ApplyAgentRoute(followUp, source, campaignId, ownerId: null, ownerName: null, _clock.UtcNow);

                return FollowUpRoutingTarget.Agent;

            case FollowUpRoutingTarget.Dialer:
                var profile = _dialerContributor is null || string.IsNullOrWhiteSpace(routing.DialerProfileId)
                    ? null
                    : await _dialerContributor.FindByIdAsync(routing.DialerProfileId.Trim());

                if (profile is null)
                {
                    _logger.LogWarning(
                        "Subject action {SubjectActionId} sends its follow-up to dialer profile '{DialerProfileId}', which does not exist or the dialer is off; the follow-up is routed as before.",
                        action.ItemId,
                        routing.DialerProfileId);

                    followUp.CampaignId = campaignId;

                    return FollowUpRoutingTarget.SameAsBefore;
                }

                var destination = await ActivityDialDestinations.FindAsync(followUp, _contentManager, _contactMethods);

                if (string.IsNullOrWhiteSpace(destination))
                {
                    _logger.LogWarning(
                        "Subject action {SubjectActionId} sends its follow-up to the dialer, but the contact has no number the dialer can call; the follow-up is routed as before.",
                        action.ItemId);

                    followUp.CampaignId = campaignId;

                    return FollowUpRoutingTarget.SameAsBefore;
                }

                FollowUpRoutes.ApplyDialerRoute(followUp, profile, campaignId, destination);

                return FollowUpRoutingTarget.Dialer;

            default:
                followUp.CampaignId = campaignId;

                return FollowUpRoutingTarget.SameAsBefore;
        }
    }

    private async Task<SubjectFlowSettings> FindFlowSettingsForSubjectAsync(string subjectContentType)
    {
        return await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(subjectContentType);
    }

    private async Task ApplyOutcomeAsync(SubjectActionExecutionContext context)
    {
        if (context.Disposition.Outcome == DispositionOutcome.NotInService)
        {
            await MarkNumberNotInServiceAsync(context);
        }
    }

    private async Task MarkNumberNotInServiceAsync(SubjectActionExecutionContext context)
    {
        var activity = context.Activity;

        // A disposition the platform applied because the network reported the number dead arrives with the number
        // already marked -- the one that was actually dialed -- so it is not marked a second time.
        if (context.NotInServiceSource is not null)
        {
            return;
        }

        // Only a call reaches a number; the destination of a message or an email says nothing about a phone line.
        if (!string.Equals(activity.Channel, OmnichannelConstants.Channels.Phone, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(activity.PreferredDestination))
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Disposition '{Disposition}' marks the number as not in service, but activity '{ActivityId}' has no phone number to mark (channel '{Channel}').",
                    context.Disposition.Name,
                    activity.ItemId,
                    activity.Channel);
            }

            return;
        }

        await _notInServiceNumbers.MarkAsync(new NotInServiceMark
        {
            PhoneNumber = activity.PreferredDestination,
            Source = OmnichannelConstants.NotInServiceSources.Agent,
            Reason = context.Disposition.Name,
            ActivityId = activity.ItemId,
            CampaignId = activity.CampaignId,
            ContactContentItemId = activity.ContactContentItemId,
            MarkedById = activity.CompletedById,
            MarkedByUsername = activity.CompletedByUsername,
        });
    }

    private async Task ApplyCommunicationPreferencesAsync(SubjectAction action, ContentItem contact)
    {
        if (contact is null)
        {
            return;
        }

        if (!action.SetDoNotCall.HasValue &&
            !action.SetDoNotEmail.HasValue &&
            !action.SetDoNotSms.HasValue)
        {
            return;
        }

        var now = _clock.UtcNow;

        contact.Alter<OmnichannelContactPart>(part =>
        {
            if (action.SetDoNotCall.HasValue)
            {
                part.SetDoNotCall(action.SetDoNotCall.Value, now);
            }

            if (action.SetDoNotEmail.HasValue)
            {
                part.SetDoNotEmail(action.SetDoNotEmail.Value, now);
            }

            if (action.SetDoNotSms.HasValue)
            {
                part.SetDoNotSms(action.SetDoNotSms.Value, now);
            }
        });

        // Altering the content item only changes the copy in memory. Nothing downstream reads that copy: the
        // preference is read back from the contact's own record, and the lists that decide who gets dialled or
        // messaged query the published one. Without these two lines a customer could ask not to be called, be
        // dispositioned exactly right, and be dialled again on the next load -- which is what happened.
        await _contentManager.UpdateAsync(contact);

        if (contact.Published)
        {
            await _contentManager.PublishAsync(contact);
        }
    }

    private ISubjectActionHandler GetHandler(string actionType)
        => _handlers.FirstOrDefault(handler => string.Equals(handler.ActionType, actionType, StringComparison.Ordinal));

    /// <summary>
    /// Moves the activity's lead to the status the action names. It does nothing for a contact, for a lead that was
    /// already converted, or for an action that names no status.
    /// </summary>
    private Task ApplyLeadStatusAsync(SubjectAction action, ContentItem contact)
        => action.TryGet<SetLeadStatusActionMetadata>(out var metadata)
            ? ApplyLeadStatusAsync(metadata.StatusId, contact)
            : Task.CompletedTask;

    private async Task ApplyLeadStatusAsync(string statusId, ContentItem contact)
    {
        if (contact is null ||
            string.IsNullOrEmpty(statusId) ||
            !contact.TryGet<LeadPart>(out var leadPart) ||
            leadPart.IsConverted ||
            string.Equals(leadPart.StatusId, statusId, StringComparison.Ordinal))
        {
            return;
        }

        contact.Alter<LeadPart>(part => part.StatusId = statusId);

        await _contentManager.UpdateAsync(contact);

        if (contact.Published)
        {
            await _contentManager.PublishAsync(contact);
        }
    }

    private static bool HasChosenScheduleDate(SubjectAction action, SubjectActionExecutionContext context)
        => context.ActionScheduleDates?.TryGetValue(action.ItemId, out var userDate) == true && userDate.HasValue;

    private async Task<DateTime> ResolveScheduleDateAsync(
        SubjectAction action,
        SubjectActionExecutionContext context,
        int? defaultScheduleHours)
    {
        if (context.ActionScheduleDates?.TryGetValue(action.ItemId, out var userDate) == true && userDate.HasValue)
        {
            if (userDate.Value.Kind == DateTimeKind.Utc)
            {
                return userDate.Value;
            }

            return await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(userDate.Value, DateTimeKind.Unspecified));
        }

        if (defaultScheduleHours.HasValue)
        {
            return _clock.UtcNow.AddHours(defaultScheduleHours.Value);
        }

        return _clock.UtcNow.AddDays(1);
    }

    private static string ResolvePreparationNotes(
        SubjectAction action,
        SubjectActionExecutionContext context,
        string fallback)
    {
        if (context.ActionPreparationNotes?.TryGetValue(action.ItemId, out var note) == true &&
            !string.IsNullOrWhiteSpace(note))
        {
            return note.Trim();
        }

        return fallback;
    }

    private async Task<bool> TryAssignOwnerAsync(
        OmnichannelActivity followUpActivity,
        SubjectAction action,
        SubjectActionOwnerAssignmentType assignmentType,
        string normalizedUserName,
        OmnichannelActivity completedActivity,
        DateTime assignedToUtc)
    {
        var effectiveAssignmentType = SubjectActionOwnerAssignmentTypeResolver.Resolve(assignmentType, normalizedUserName);

        if (effectiveAssignmentType == SubjectActionOwnerAssignmentType.SameOwner)
        {
            AssignOwner(
                followUpActivity,
                completedActivity.CompletedById,
                completedActivity.CompletedByUsername,
                assignedToUtc);

            return true;
        }

        if (effectiveAssignmentType == SubjectActionOwnerAssignmentType.CallAgent)
        {
            await AssignCallAgentAsync(followUpActivity, completedActivity, assignedToUtc);

            return true;
        }

        if (effectiveAssignmentType == SubjectActionOwnerAssignmentType.Pool)
        {
            await AssignFromPoolAsync(followUpActivity, action, assignedToUtc);

            return true;
        }

        var owner = await _session.Query<User, UserIndex>(x => x.NormalizedUserName == normalizedUserName).FirstOrDefaultAsync();

        if (owner is null)
        {
            _logger.LogWarning(
                "The configured specific owner {NormalizedUserName} for subject action {SubjectActionId} was not found. Skipping follow-up activity creation.",
                normalizedUserName,
                action.ItemId);

            return false;
        }

        AssignOwner(followUpActivity, owner.UserId, owner.UserName, assignedToUtc);

        return true;
    }

    /// <summary>
    /// Gives a manual follow-up that would otherwise have no owner to the agent the call was placed or connected for.
    /// </summary>
    /// <remarks>
    /// "The user who completed the activity" names nobody when the dialer completed it, as it does when nobody answers
    /// or a machine does, and the follow-up an agent was to work by hand was left with no owner. The agent the dialer
    /// held or connected for the call is the one who calls back. Work the dialer queues is offered to whoever is free, so
    /// it is left without an owner.
    /// </remarks>
    private async Task FillMissingOwnerAsync(
        OmnichannelActivity followUpActivity,
        SubjectAction action,
        SubjectActionOwnerAssignmentType assignmentType,
        string normalizedUserName,
        OmnichannelActivity completedActivity,
        DateTime assignedToUtc)
    {
        // An automated (AI) follow-up runs on its own and needs no owner.
        if (!string.IsNullOrEmpty(followUpActivity.AssignedToId) ||
            followUpActivity.InteractionType != ActivityInteractionType.Manual ||
            ActivitySources.IsDialer(followUpActivity.Source) ||
            SubjectActionOwnerAssignmentTypeResolver.Resolve(assignmentType, normalizedUserName) != SubjectActionOwnerAssignmentType.SameOwner)
        {
            return;
        }

        var userId = _callAgentResolver is null
            ? null
            : await _callAgentResolver.FindAgentUserIdAsync(completedActivity.ItemId);

        var agent = string.IsNullOrEmpty(userId)
            ? null
            : await _session.Query<User, UserIndex>(x => x.UserId == userId).FirstOrDefaultAsync();

        if (agent is null)
        {
            _logger.LogWarning(
                "Nobody completed activity {ActivityId} and no agent was on its call, so the follow-up of subject action {SubjectActionId} has no owner; any agent can take it.",
                completedActivity.ItemId,
                action.ItemId);

            return;
        }

        AssignOwner(followUpActivity, agent.UserId, agent.UserName, assignedToUtc);

        if (followUpActivity.Status is ActivityStatus.Scheduled or ActivityStatus.NotStated)
        {
            followUpActivity.Status = OmnichannelAutomationHelper.GetInitialActivityStatus(followUpActivity.InteractionType, hasAssignedUser: true);
        }
    }

    /// <summary>
    /// Gives the follow-up to the agent the call was placed or connected for. A Power dial that reached an answering
    /// machine is dispositioned by the system, so nobody completed it; the agent the dialer held for the call is still
    /// on the call, and is the one who calls back. With no such agent, whoever completed the activity owns it.
    /// </summary>
    private async Task AssignCallAgentAsync(OmnichannelActivity followUpActivity, OmnichannelActivity completedActivity, DateTime assignedToUtc)
    {
        var userId = _callAgentResolver is null
            ? null
            : await _callAgentResolver.FindAgentUserIdAsync(completedActivity.ItemId);

        if (!string.IsNullOrEmpty(userId))
        {
            var agent = await _session.Query<User, UserIndex>(x => x.UserId == userId).FirstOrDefaultAsync();

            if (agent is not null)
            {
                AssignOwner(followUpActivity, agent.UserId, agent.UserName, assignedToUtc);

                return;
            }
        }

        AssignOwner(followUpActivity, completedActivity.CompletedById, completedActivity.CompletedByUsername, assignedToUtc);
    }

    /// <summary>
    /// Gives the follow-up to one user of the action's pool. A pool with nobody to pick leaves the follow-up for any
    /// agent rather than dropping it.
    /// </summary>
    private async Task AssignFromPoolAsync(OmnichannelActivity followUpActivity, SubjectAction action, DateTime assignedToUtc)
    {
        if (_ownerPicker is null || !action.TryGet<FollowUpOwnerPoolMetadata>(out var pool))
        {
            _logger.LogWarning(
                "Subject action {SubjectActionId} assigns its follow-up from a pool, but {Reason}; the follow-up is left for any agent.",
                action.ItemId,
                _ownerPicker is null ? "the Teams feature is off" : "it names no pool");

            AssignOwner(followUpActivity, null, null, assignedToUtc);

            return;
        }

        var (userId, userName) = await _ownerPicker.PickAsync(pool);

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning(
                "The pool of subject action {SubjectActionId} has nobody to give the follow-up to; it is left for any agent.",
                action.ItemId);
        }

        AssignOwner(followUpActivity, userId, userName, assignedToUtc);
    }

    private static void AssignOwner(
        OmnichannelActivity activity,
        string ownerId,
        string ownerName,
        DateTime assignedToUtc)
    {
        activity.AssignedToId = ownerId;
        activity.AssignedToUsername = ownerName;

        if (!string.IsNullOrWhiteSpace(ownerId) || !string.IsNullOrWhiteSpace(ownerName))
        {
            activity.AssignedToUtc = assignedToUtc;
            activity.AssignmentStatus = ActivityAssignmentStatus.Assigned;
        }
    }
}
