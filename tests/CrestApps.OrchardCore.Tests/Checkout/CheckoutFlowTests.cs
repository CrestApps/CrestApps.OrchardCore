using CrestApps.OrchardCore.Checkout;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Checkout;

public sealed class CheckoutFlowTests
{
    [Fact]
    public void GetSortedSteps_OrdersByOrderThenDeclaration_AndHidesConcealed()
    {
        // Arrange
        var session = new CheckoutSession { SessionId = "s1" };
        session.Steps.Add(new CheckoutFlowStep { Key = "b", Order = 2 });
        session.Steps.Add(new CheckoutFlowStep { Key = "a", Order = 1 });
        session.Steps.Add(new CheckoutFlowStep { Key = "hidden", Order = 3, Conceal = true });

        var flow = new CheckoutFlow(session);

        // Act
        var steps = flow.GetSortedSteps();

        // Assert
        Assert.Equal(2, steps.Length);
        Assert.Equal("a", steps[0].Key);
        Assert.Equal("b", steps[1].Key);
    }

    [Fact]
    public void GetNextStep_ReturnsFollowingStep_AndNullOnLast()
    {
        // Arrange
        var session = new CheckoutSession { SessionId = "s1", CurrentStep = "a" };
        session.Steps.Add(new CheckoutFlowStep { Key = "a", Order = 1 });
        session.Steps.Add(new CheckoutFlowStep { Key = "b", Order = 2 });

        var flow = new CheckoutFlow(session);

        // Act & Assert
        Assert.Equal("b", flow.GetNextStep().Key);

        flow.SetCurrentStep("b");
        Assert.Null(flow.GetNextStep());
    }

    [Fact]
    public void GetPreviousStep_RequiresSavedSteps()
    {
        // Arrange
        var session = new CheckoutSession { SessionId = "s1", CurrentStep = "b" };
        session.Steps.Add(new CheckoutFlowStep { Key = "a", Order = 1 });
        session.Steps.Add(new CheckoutFlowStep { Key = "b", Order = 2 });

        var flow = new CheckoutFlow(session);

        // Act & Assert: without saved steps there is no navigable previous step.
        Assert.Null(flow.GetPreviousStep());

        session.SavedSteps["a"] = true;
        Assert.Equal("a", flow.GetPreviousStep().Key);
    }

    [Fact]
    public void SetCurrentStep_WhenStepMissing_Throws()
    {
        // Arrange
        var session = new CheckoutSession { SessionId = "s1" };
        session.Steps.Add(new CheckoutFlowStep { Key = "a", Order = 1 });

        var flow = new CheckoutFlow(session);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => flow.SetCurrentStep("missing"));
    }
}
