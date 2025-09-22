using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
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

public sealed class TryAgainActivityTask : TaskActivity<TryAgainActivityTask>
{
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public TryAgainActivityTask(
        ISession session,
        IClock clock,
        IStringLocalizer<TryAgainActivityTask> stringLocalizer)
    {
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["Try Activity Again Task"];

    public override LocalizedString Category => S["Omnichannel"];

    public int? MaxAttempt
    {
        get => GetProperty<int?>();
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

        if (activity is null)
        {
            return Outcomes("Done");
        }

        if (MaxAttempt.HasValue && activity.Attempts >= MaxAttempt.Value)
        {
            return Outcomes("Done");
        }

        var now = _clock.UtcNow;
        var nextAttempt = new OmnichannelActivity()
        {
            ActivityId = IdGenerator.GenerateId(),
            Channel = activity.Channel,
            ChannelEndpoint = activity.ChannelEndpoint,
            InteractionType = activity.InteractionType,
            PreferredDestination = activity.PreferredDestination,
            AIProfileName = activity.AIProfileName,
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
            UrgencyLevel = UrgencyLevel ?? activity.UrgencyLevel,
            Status = ActivityStatus.NotStated,
        };

        if (activity.TryGet<DispositionMetadata>(out var dispositionMetadata) && dispositionMetadata.ScheduledDate.HasValue)
        {
            nextAttempt.ScheduledUtc = dispositionMetadata.ScheduledDate.Value;
        }
        else if (DefaultScheduleHours.HasValue)
        {
            nextAttempt.ScheduledUtc = now.AddHours(DefaultScheduleHours.Value);
        }
        else
        {
            nextAttempt.ScheduledUtc = now.AddDays(1);
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

        workflowContext.Output["Activity"] = nextAttempt;

        await _session.SaveAsync(nextAttempt, collection: OmnichannelConstants.CollectionName);

        return Outcomes("Done");
    }
}
