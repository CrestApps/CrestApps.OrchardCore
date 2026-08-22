using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

public class SubscriptionFlowTests
{
    private static readonly SubscriptionFlowStep[] _steps =
    [
        new SubscriptionFlowStep
        {
            Key = "one"
        },
        new SubscriptionFlowStep
        {
            Key = "two"
        },
        new SubscriptionFlowStep
        {
            Key = "three"
        },
    ];

    [Theory]
    [InlineData(null, "one")]
    [InlineData("one", "one")]
    [InlineData("two", "two")]
    [InlineData("three", "three")]
    [InlineData("ten", "one")]
    public void GetCurrentStep_WhenCalled_ReturnCorrectStep(string currentStep, string actualStep)
    {
        var session = GetSession(currentStep);

        var flow = new SubscriptionFlow(session, new ContentItem());

        Assert.Equal(actualStep, flow.GetCurrentStep()?.Key);
    }

    [Theory]
    [InlineData(null, "two")]
    [InlineData("one", "two")]
    [InlineData("two", "three")]
    [InlineData("three", null)]
    [InlineData("ten", "two")]
    public void GetNextStep_WhenCalled_ReturnCorrectStep(string currentStep, string nextStep)
    {
        var session = GetSession(currentStep);

        var flow = new SubscriptionFlow(session, new ContentItem());

        Assert.Equal(nextStep, flow.GetNextStep()?.Key);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("one", null)]
    [InlineData("two", "one")]
    [InlineData("three", "two")]
    [InlineData("ten", null)]
    public void GetPreviousStep_WhenCalled_ReturnCorrectStep(string currentStep, string previousStep)
    {
        var data = new JsonObject();
        foreach (var item in _steps)
        {
            // The value here is irrelevant.
            data[item.Key] = string.Empty;
        }
        var session = GetSession(currentStep, data);

        var flow = new SubscriptionFlow(session, new ContentItem());

        Assert.Equal(previousStep, flow.GetPreviousStep()?.Key);
    }

    [Fact]
    public void GetPreviousStep_WhenNoSavedDataIsNull_ReturnsNull()
    {
        var session = GetSession("two");

        var flow = new SubscriptionFlow(session, new ContentItem());

        Assert.Null(flow.GetPreviousStep());
    }

    [Fact]
    public void GetPreviousStep_WhenNoSavedDataIsEmpty_ReturnsNull()
    {
        var session = GetSession("two", savedData: []);

        var flow = new SubscriptionFlow(session, new ContentItem());

        Assert.Null(flow.GetPreviousStep());
    }

    private static ISubscriptionFlowSession GetSession(string currentStep, JsonObject savedData = null)
    {
        var sessionMock = new Mock<ISubscriptionFlowSession>();
        sessionMock.Setup(session => session.Steps).Returns(_steps);
        sessionMock.Setup(session => session.CurrentStep).Returns(currentStep);

        if (savedData != null)
        {
            sessionMock.Setup(session => session.SavedSteps).Returns(savedData);
        }

        return sessionMock.Object;
    }
}
