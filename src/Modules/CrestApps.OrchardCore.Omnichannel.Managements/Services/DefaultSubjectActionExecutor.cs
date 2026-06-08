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
    private readonly ICatalog<SubjectFlowSettings> _flowSettingsCatalog;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultSubjectActionExecutor(
        ISourceCatalog<SubjectAction> actionCatalog,
        ICatalog<SubjectFlowSettings> flowSettingsCatalog,
        IContentManager contentManager,
        ISession session,
        IClock clock,
        ILogger<DefaultSubjectActionExecutor> logger)
    {
        _actionCatalog = actionCatalog;
        _flowSettingsCatalog = flowSettingsCatalog;
        _contentManager = contentManager;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(SubjectActionExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Activity);
        ArgumentNullException.ThrowIfNull(context.Disposition);

        var allActions = await _actionCatalog.GetAllAsync(cancellationToken);

        var actions = allActions
            .Where(a => string.Equals(a.SubjectContentType, context.Activity.SubjectContentType, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.DispositionId, context.Disposition.ItemId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var action in actions)
        {
            await ExecuteActionAsync(action, context);
        }
    }

    private async Task ExecuteActionAsync(SubjectAction action, SubjectActionExecutionContext context)
    {
        ApplyCommunicationPreferences(action, context.Contact);

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
                _logger.LogWarning("Unknown subject action type: {ActionType}", action.Source);
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
        var nextAttempt = new OmnichannelActivity
        {
            ItemId = IdGenerator.GenerateId(),
            Channel = activity.Channel,
            ChannelEndpointId = activity.ChannelEndpointId,
            InteractionType = activity.InteractionType,
            PreferredDestination = activity.PreferredDestination,
            ContactContentItemId = activity.ContactContentItemId,
            ContactContentType = activity.ContactContentType,
            CampaignId = activity.CampaignId,
            Instructions = activity.Instructions,
            Attempts = activity.Attempts + 1,
            AssignedToId = activity.CompletedById,
            AssignedToUsername = activity.CompletedByUsername,
            AssignedToUtc = now,
            CreatedById = activity.CompletedById,
            CreatedByUsername = activity.CompletedByUsername,
            CreatedUtc = now,
            SubjectContentType = activity.SubjectContentType,
            Subject = activity.Subject,
            UrgencyLevel = metadata.UrgencyLevel ?? activity.UrgencyLevel,
            Status = ActivityStatus.NotStated,
        };

        nextAttempt.ScheduledUtc = ResolveScheduleDate(action, context, metadata.DefaultScheduleHours);

        await ResolveAssigneeAsync(nextAttempt, metadata.NormalizedUserName);

        await _session.SaveAsync(nextAttempt, collection: OmnichannelConstants.CollectionName);
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
            CampaignId = activity.CampaignId,
            Instructions = null,
            Attempts = 1,
            AssignedToId = activity.CompletedById,
            AssignedToUsername = activity.CompletedByUsername,
            AssignedToUtc = now,
            CreatedById = activity.CompletedById,
            CreatedByUsername = activity.CompletedByUsername,
            CreatedUtc = now,
            SubjectContentType = targetSubjectContentType,
            UrgencyLevel = metadata.UrgencyLevel ?? activity.UrgencyLevel,
            Status = ActivityStatus.NotStated,
        };

        newActivity.Subject = await _contentManager.NewAsync(targetSubjectContentType);

        if (flowSettings != null)
        {
            newActivity.Channel = flowSettings.Channel ?? activity.Channel;
            newActivity.InteractionType = flowSettings.InteractionType;
            newActivity.ChannelEndpointId = flowSettings.ChannelEndpointId ?? activity.ChannelEndpointId;
            newActivity.CampaignId = flowSettings.CampaignId ?? activity.CampaignId;
        }

        newActivity.ScheduledUtc = ResolveScheduleDate(action, context, metadata.DefaultScheduleHours);

        await ResolveAssigneeAsync(newActivity, metadata.NormalizedUserName);

        if (context.Contact is not null)
        {
            newActivity.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(context.Contact, newActivity.Channel);
        }

        await _session.SaveAsync(newActivity, collection: OmnichannelConstants.CollectionName);
    }

    private async Task<SubjectFlowSettings> FindFlowSettingsForSubjectAsync(string subjectContentType)
    {
        var allFlowSettings = await _flowSettingsCatalog.GetAllAsync();

        return allFlowSettings.FirstOrDefault(f =>
            string.Equals(f.SubjectContentType, subjectContentType, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyCommunicationPreferences(SubjectAction action, ContentItem contact)
    {
        if (contact is null)
        {
            return;
        }

        if (!action.SetDoNotCall.HasValue &&
            !action.SetDoNotEmail.HasValue &&
            !action.SetDoNotSms.HasValue &&
            !action.SetDoNotChat.HasValue)
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

            if (action.SetDoNotChat.HasValue)
            {
                part.SetDoNotChat(action.SetDoNotChat.Value, now);
            }
        });
    }

    private DateTime ResolveScheduleDate(SubjectAction action, SubjectActionExecutionContext context, int? defaultScheduleHours)
    {
        if (context.ActionScheduleDates?.TryGetValue(action.ItemId, out var userDate) == true && userDate.HasValue)
        {
            return userDate.Value;
        }

        if (defaultScheduleHours.HasValue)
        {
            return _clock.UtcNow.AddHours(defaultScheduleHours.Value);
        }

        return _clock.UtcNow.AddDays(1);
    }

    private async Task ResolveAssigneeAsync(OmnichannelActivity activity, string normalizedUserName)
    {
        if (string.IsNullOrEmpty(normalizedUserName))
        {
            return;
        }

        var owner = await _session.Query<User, UserIndex>(x => x.NormalizedUserName == normalizedUserName).FirstOrDefaultAsync();

        if (owner is not null)
        {
            activity.AssignedToId = owner.UserId;
            activity.AssignedToUsername = owner.UserName;
        }
    }
}
