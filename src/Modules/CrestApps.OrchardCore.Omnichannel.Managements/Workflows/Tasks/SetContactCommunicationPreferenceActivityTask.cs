using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Tasks;

public sealed class SetContactCommunicationPreferenceActivityTask : TaskActivity<TryAgainActivityTask>
{
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public SetContactCommunicationPreferenceActivityTask(
        ISession session,
        IClock clock,
        IStringLocalizer<SetContactCommunicationPreferenceActivityTask> stringLocalizer)
    {
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    public override LocalizedString DisplayText => S["Set Contact Communication Preferences Task"];

    public override LocalizedString Category => S["Omnichannel"];

    public bool? SetDoNotCall
    {
        get => GetProperty<bool?>();
        set => SetProperty(value);
    }

    public bool? SetDoNotSms
    {
        get => GetProperty<bool?>();
        set => SetProperty(value);
    }

    public bool? SetDoNotEmail
    {
        get => GetProperty<bool?>();
        set => SetProperty(value);
    }

    public bool? SetDoNotChat
    {
        get => GetProperty<bool?>();
        set => SetProperty(value);
    }

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes(S["Done"]);
    }

    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var contact = workflowContext.Input["Contact"] as ContentItem;

        if (contact is null)
        {
            return Outcomes("Done");
        }

        if (!SetDoNotCall.HasValue &&
           !SetDoNotEmail.HasValue &&
           !SetDoNotSms.HasValue &&
           !SetDoNotChat.HasValue)
        {
            return Outcomes("Done");
        }

        contact.Alter<CommunicationPreferencePart>(part =>
        {
            if (SetDoNotCall.HasValue)
            {
                part.DoNotCall = SetDoNotCall.Value;
                part.DoNotCallUtc = _clock.UtcNow;
            }

            if (SetDoNotEmail.HasValue)
            {
                part.DoNotEmail = SetDoNotEmail.Value;
                part.DoNotEmailUtc = _clock.UtcNow;
            }

            if (SetDoNotSms.HasValue)
            {
                part.DoNotSms = SetDoNotSms.Value;
                part.DoNotSmsUtc = _clock.UtcNow;
            }

            if (SetDoNotChat.HasValue)
            {
                part.DoNotChat = SetDoNotChat.Value;
                part.DoNotChatUtc = _clock.UtcNow;
            }
        });

        await _session.SaveAsync(contact);

        return Outcomes("Done");
    }
}
