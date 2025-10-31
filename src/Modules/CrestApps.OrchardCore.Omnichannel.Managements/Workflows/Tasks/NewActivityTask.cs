using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Tasks;

public sealed class NewActivityTask : TaskActivity<NewActivityTask>
{
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly IContentManager _contentManager;
    private readonly ICatalog<OmnichannelCampaign> _campaignCatalog;

    internal readonly IStringLocalizer S;

    public NewActivityTask(
        ISession session,
        IClock clock,
        IContentManager contentManager,
        ICatalog<OmnichannelCampaign> campaignCatalog,
        IStringLocalizer<TryAgainActivityTask> stringLocalizer)
    {
        _session = session;
        _clock = clock;
        _contentManager = contentManager;
        _campaignCatalog = campaignCatalog;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["New Activity Task"];

    public override LocalizedString Category => S["Omnichannel"];

    public string CampaignId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public string SubjectContentType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public ActivityUrgencyLevel? UrgencyLevel
    {
        get => GetProperty<ActivityUrgencyLevel?>();
        set => SetProperty(value);
    }

    public string NormalizedUserName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public int? DefaultScheduleHours
    {
        get => GetProperty<int?>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes(S["Done"]);
    }

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var activity = workflowContext.Input["Activity"] as OmnichannelActivity;
        if (activity is null || string.IsNullOrEmpty(SubjectContentType))
        {
            return Outcomes("Done");
        }

        var now = _clock.UtcNow;
        var newAttempt = new OmnichannelActivity()
        {
            ItemId = IdGenerator.GenerateId(),
            Channel = activity.Channel,
            ChannelEndpointId = activity.ChannelEndpointId,
            InteractionType = activity.InteractionType,
            PreferredDestination = activity.PreferredDestination,
            AIProfileName = activity.AIProfileName,
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
            UrgencyLevel = UrgencyLevel ?? activity.UrgencyLevel,
            Status = ActivityStatus.NotStated,
        };

        if (!string.IsNullOrEmpty(SubjectContentType))
        {
            newAttempt.SubjectContentType = SubjectContentType;
            newAttempt.Subject = await _contentManager.NewAsync(SubjectContentType);
        }
        else
        {
            newAttempt.SubjectContentType = activity.SubjectContentType;
            newAttempt.Subject = await _contentManager.NewAsync(activity.SubjectContentType);
        }

        if (activity.TryGet<DispositionMetadata>(out var dispositionMetadata) && dispositionMetadata.ScheduledDate.HasValue)
        {
            newAttempt.ScheduledUtc = dispositionMetadata.ScheduledDate.Value;
        }
        else if (DefaultScheduleHours.HasValue)
        {
            newAttempt.ScheduledUtc = now.AddHours(DefaultScheduleHours.Value);
        }
        else
        {
            newAttempt.ScheduledUtc = now.AddDays(1);
        }

        if (!string.IsNullOrEmpty(CampaignId))
        {
            var campaign = await _campaignCatalog.FindByIdAsync(CampaignId);

            if (campaign != null)
            {
                newAttempt.Channel = campaign.Channel;
                newAttempt.InteractionType = campaign.InteractionType;
                newAttempt.ChannelEndpointId = campaign.ChannelEndpointId;
                newAttempt.CampaignId = campaign.ItemId;
            }
        }

        if (!string.IsNullOrEmpty(NormalizedUserName))
        {
            var owner = await _session.Query<User, UserIndex>(x => x.NormalizedUserName == NormalizedUserName).FirstOrDefaultAsync();

            if (owner is not null)
            {
                activity.AssignedToId = owner.UserId;
                activity.AssignedToUsername = owner.UserName;
            }
        }

        var contact = workflowContext.Input["Contact"] as ContentItem;

        if (contact is not null)
        {
            newAttempt.PreferredDestination = OmnichannelHelper.GetPreferredDestenation(contact, newAttempt.Channel);
        }

        workflowContext.Output["Activity"] = newAttempt;

        await _session.SaveAsync(newAttempt, collection: OmnichannelConstants.CollectionName);

        return Outcomes("Done");
    }
}
