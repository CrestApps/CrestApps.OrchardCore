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

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        if (string.IsNullOrWhiteSpace(CampaignId))
        {
            yield return Outcome(S["Invalid Campaign"]);
            yield break;
        }

        var campaign = _campaignsCatalog.FindByIdAsync(CampaignId).AsTask().GetAwaiter().GetResult();

        if (campaign == null)
        {
            yield return Outcome(S["Invalid Campaign"]);
            yield break;
        }

        var dispositionIds = campaign.DispositionIds ?? [];

        var dispositions = _dispositionsCatalog.GetAsync(dispositionIds).AsTask().GetAwaiter().GetResult();

        foreach (var disposition in dispositions.OrderBy(x => x.DisplayText))
        {
            yield return Outcome(new LocalizedString(disposition.DisplayText, disposition.DisplayText));
        }

        yield return Outcome(S["Done"]);
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
