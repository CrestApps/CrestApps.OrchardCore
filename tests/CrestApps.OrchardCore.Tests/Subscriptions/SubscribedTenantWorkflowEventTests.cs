using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class SubscribedTenantWorkflowEventTests
{
    [Fact]
    public void SucceededEvent_HasExpectedNameAndCategory()
    {
        var activity = new SubscribedTenantSetupSucceededEvent(new PassThroughStringLocalizer<SubscribedTenantSetupSucceededEvent>());

        Assert.Equal("SubscribedTenantSetupSucceededEvent", activity.Name);
        Assert.Equal(SubscribedTenantSetupSucceededEvent.EventName, activity.Name);
        Assert.Equal("Subscriptions", activity.Category.Value);
    }

    [Fact]
    public void FailedEvent_HasExpectedNameAndCategory()
    {
        var activity = new SubscribedTenantFailedSetupEvent(new PassThroughStringLocalizer<SubscribedTenantFailedSetupEvent>());

        Assert.Equal("SubscribedTenantFailedSetupEvent", activity.Name);
        Assert.Equal(SubscribedTenantFailedSetupEvent.EventName, activity.Name);
        Assert.Equal("Subscriptions", activity.Category.Value);
    }

    [Fact]
    public void SucceededEvent_GetPossibleOutcomes_ReturnsDone()
    {
        var activity = new SubscribedTenantSetupSucceededEvent(new PassThroughStringLocalizer<SubscribedTenantSetupSucceededEvent>());

        var outcomes = activity.GetPossibleOutcomes(null, null).ToList();

        Assert.Single(outcomes);
        Assert.Equal("Done", outcomes[0].DisplayName.Value);
    }

    [Fact]
    public void FailedEvent_GetPossibleOutcomes_ReturnsDone()
    {
        var activity = new SubscribedTenantFailedSetupEvent(new PassThroughStringLocalizer<SubscribedTenantFailedSetupEvent>());

        var outcomes = activity.GetPossibleOutcomes(null, null).ToList();

        Assert.Single(outcomes);
        Assert.Equal("Done", outcomes[0].DisplayName.Value);
    }

    [Fact]
    public void SucceededEvent_Resume_ReturnsDoneOutcome()
    {
        var activity = new SubscribedTenantSetupSucceededEvent(new PassThroughStringLocalizer<SubscribedTenantSetupSucceededEvent>());

        var result = activity.Resume(null, null);

        Assert.Contains("Done", result.Outcomes);
    }

    [Fact]
    public void FailedEvent_Resume_ReturnsDoneOutcome()
    {
        var activity = new SubscribedTenantFailedSetupEvent(new PassThroughStringLocalizer<SubscribedTenantFailedSetupEvent>());

        var result = activity.Resume(null, null);

        Assert.Contains("Done", result.Outcomes);
    }
}
