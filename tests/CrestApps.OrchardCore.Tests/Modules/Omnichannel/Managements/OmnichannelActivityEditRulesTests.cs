using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;

/// <summary>
/// What saving the activity editor may change about how an activity is carried out.
/// </summary>
public sealed class OmnichannelActivityEditRulesTests
{
    [Fact]
    public void RescheduledActivity_KeepsItsOwnDelivery()
    {
        // Arrange
        // Live: an automated call's retry was rescheduled from the editor and came back a manual task with no
        // channel, because the save wrote the subject's defaults over the settings the call was loaded with.
        var activity = new OmnichannelActivity { SubjectContentType = "LeadGeneration" };

        // Act
        var applies = OmnichannelActivityEditRules.AppliesFlowDelivery(activity, "LeadGeneration", isNew: false);

        // Assert
        Assert.False(applies);
    }

    [Fact]
    public void NewActivity_TakesTheSubjectsDelivery()
    {
        // Act
        var applies = OmnichannelActivityEditRules.AppliesFlowDelivery(new OmnichannelActivity(), "LeadGeneration", isNew: true);

        // Assert
        Assert.True(applies);
    }

    [Fact]
    public void ActivityMovedToAnotherSubject_TakesThatSubjectsDelivery()
    {
        // Arrange
        var activity = new OmnichannelActivity { SubjectContentType = "LeadGeneration" };

        // Act
        var applies = OmnichannelActivityEditRules.AppliesFlowDelivery(activity, "LeadGenerationFollowUp30Days", isNew: false);

        // Assert
        Assert.True(applies);
    }
}
