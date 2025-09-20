using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Events;

public sealed class CompletedActivityEvent : EventActivity
{
    private readonly ICatalog<OmnichannelCampaign> _campaignsCatalog;
    private readonly ICatalog<OmnichannelDisposition> _dispositionsCatalog;

    internal readonly IStringLocalizer S;

    public CompletedActivityEvent(
        ICatalog<OmnichannelCampaign> campaignsCatalog,
        ICatalog<OmnichannelDisposition> dispositionsCatalog,
        IStringLocalizer<CompletedActivityEvent> stringLocalizer)
    {
        _campaignsCatalog = campaignsCatalog;
        _dispositionsCatalog = dispositionsCatalog;
        S = stringLocalizer;
    }

    public override string Name
        => nameof(CompletedActivityEvent);

    public override LocalizedString DisplayText => S["Completed Omnichannel Activity"];

    public override LocalizedString Category => S["Omnichannel"];

    public string CampaignId
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    public override async ValueTask<IEnumerable<Outcome>> GetPossibleOutcomesAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (string.IsNullOrWhiteSpace(CampaignId))
        {
            return [Outcome(S["Invalid Campaign"])];
        }

        var campaign = await _campaignsCatalog.FindByIdAsync(CampaignId);

        if (campaign == null)
        {
            return [Outcome(S["Invalid Campaign"])];
        }

        var dispositionIds = campaign.DispositionIds ?? [];

        var dispositions = await _dispositionsCatalog.GetAsync(dispositionIds);

        var outcomes = new List<Outcome>();
        foreach (var disposition in dispositions.OrderBy(x => x.DisplayText))
        {
            outcomes.Add(Outcome(new LocalizedString(disposition.DisplayText, disposition.DisplayText)));
        }

        outcomes.Add(Outcome(S["Done"]));

        return outcomes;
    }

    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return Outcomes("Done");
    }

    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var activity = workflowContext.Input["Activity"] as OmnichannelActivity;

        if (string.IsNullOrEmpty(activity?.DispositionId) || activity.Status != ActivityStatus.Completed)
        {
            return Outcomes("Done");
        }

        var disposition = workflowContext.Input["Disposition"] as OmnichannelDisposition;

        if (disposition is null)
        {
            return Outcomes("Done");
        }

        return Outcomes(disposition.DisplayText);
    }
}
