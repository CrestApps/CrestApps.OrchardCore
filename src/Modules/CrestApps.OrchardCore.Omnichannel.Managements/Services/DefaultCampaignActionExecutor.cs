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

internal sealed class DefaultCampaignActionExecutor : ICampaignActionExecutor
{
    private readonly ISourceCatalog<CampaignAction> _actionCatalog;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public DefaultCampaignActionExecutor(
        ISourceCatalog<CampaignAction> actionCatalog,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        IContentManager contentManager,
        ISession session,
        IClock clock,
        ILogger<DefaultCampaignActionExecutor> logger)
    {
        _actionCatalog = actionCatalog;
        _campaignCatalog = campaignCatalog;
        _contentManager = contentManager;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CampaignActionExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Activity);
        ArgumentNullException.ThrowIfNull(context.Disposition);

        var allActions = await _actionCatalog.GetAllAsync();

        var actions = allActions
            .Where(a => string.Equals(a.CampaignId, context.Activity.CampaignId, StringComparison.OrdinalIgnoreCase)
                     && string.Equals(a.DispositionId, context.Disposition.ItemId, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var action in actions)
        {
            await ExecuteActionAsync(action, context);
        }
    }

    private async Task ExecuteActionAsync(CampaignAction action, CampaignActionExecutionContext context)
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
                _logger.LogWarning("Unknown campaign action type: {ActionType}", action.Source);
                break;
        }
    }

    private async Task ExecuteTryAgainAsync(CampaignAction action, CampaignActionExecutionContext context)
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

    private async Task ExecuteNewActivityAsync(CampaignAction action, CampaignActionExecutionContext context)
    {
        var activity = context.Activity;

        if (!action.TryGet<NewActivityActionMetadata>(out var metadata))
        {
            metadata = new NewActivityActionMetadata();
        }

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
            UrgencyLevel = metadata.UrgencyLevel ?? activity.UrgencyLevel,
            Status = ActivityStatus.NotStated,
        };

        var subjectContentType = !string.IsNullOrEmpty(metadata.SubjectContentType)
            ? metadata.SubjectContentType
            : activity.SubjectContentType;

        newActivity.SubjectContentType = subjectContentType;
        newActivity.Subject = await _contentManager.NewAsync(subjectContentType);

        if (!string.IsNullOrEmpty(metadata.CampaignId))
        {
            var targetCampaign = await _campaignCatalog.FindByIdAsync(metadata.CampaignId);

            if (targetCampaign != null)
            {
                newActivity.Channel = targetCampaign.Channel;
                newActivity.InteractionType = targetCampaign.InteractionType;
                newActivity.ChannelEndpointId = targetCampaign.ChannelEndpointId;
                newActivity.CampaignId = targetCampaign.ItemId;
            }
        }

        newActivity.ScheduledUtc = ResolveScheduleDate(action, context, metadata.DefaultScheduleHours);

        await ResolveAssigneeAsync(newActivity, metadata.NormalizedUserName);

        if (context.Contact is not null)
        {
            newActivity.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(context.Contact, newActivity.Channel);
        }

        await _session.SaveAsync(newActivity, collection: OmnichannelConstants.CollectionName);
    }

    private void ApplyCommunicationPreferences(CampaignAction action, ContentItem contact)
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

        contact.Alter<CommunicationPreferencePart>(part =>
        {
            if (action.SetDoNotCall.HasValue)
            {
                part.DoNotCall = action.SetDoNotCall.Value;
                part.DoNotCallUtc = now;
            }

            if (action.SetDoNotEmail.HasValue)
            {
                part.DoNotEmail = action.SetDoNotEmail.Value;
                part.DoNotEmailUtc = now;
            }

            if (action.SetDoNotSms.HasValue)
            {
                part.DoNotSms = action.SetDoNotSms.Value;
                part.DoNotSmsUtc = now;
            }

            if (action.SetDoNotChat.HasValue)
            {
                part.DoNotChat = action.SetDoNotChat.Value;
                part.DoNotChatUtc = now;
            }
        });
    }

    private DateTime ResolveScheduleDate(CampaignAction action, CampaignActionExecutionContext context, int? defaultScheduleHours)
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
