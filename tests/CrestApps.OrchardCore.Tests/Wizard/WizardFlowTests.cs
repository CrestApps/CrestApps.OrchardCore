using CrestApps.OrchardCore.Wizard;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Wizard;

public sealed class WizardFlowTests
{
    [Fact]
    public void GetSortedSteps_OrdersByOrderThenDeclaration_AndExcludesConcealed()
    {
        // Arrange
        var session = new WizardSession();
        session.Steps.Add(new WizardStep { Key = "b", Order = 10 });
        session.Steps.Add(new WizardStep { Key = "a", Order = 5 });
        session.Steps.Add(new WizardStep { Key = "hidden", Order = 1, Conceal = true });
        session.Steps.Add(new WizardStep { Key = "c", Order = 10 });

        var flow = new WizardFlow(session);

        // Act
        var sorted = flow.GetSortedSteps();

        // Assert
        Assert.Equal(["a", "b", "c"], sorted.Select(s => s.Key));
    }

    [Fact]
    public void GetCurrentStep_WhenNoCurrentStepSet_ReturnsFirstVisibleStep()
    {
        // Arrange
        var session = new WizardSession();
        session.Steps.Add(new WizardStep { Key = "first", Order = 1 });
        session.Steps.Add(new WizardStep { Key = "second", Order = 2 });

        var flow = new WizardFlow(session);

        // Act
        var current = flow.GetCurrentStep();

        // Assert
        Assert.Equal("first", current.Key);
    }

    [Fact]
    public void GetNextStep_FromCurrent_ReturnsFollowingStep()
    {
        // Arrange
        var session = new WizardSession
        {
            CurrentStep = "first",
        };
        session.Steps.Add(new WizardStep { Key = "first", Order = 1 });
        session.Steps.Add(new WizardStep { Key = "second", Order = 2 });

        var flow = new WizardFlow(session);

        // Act
        var next = flow.GetNextStep();

        // Assert
        Assert.Equal("second", next.Key);
    }

    [Fact]
    public void GetNextStep_OnLastStep_ReturnsNull()
    {
        // Arrange
        var session = new WizardSession
        {
            CurrentStep = "second",
        };
        session.Steps.Add(new WizardStep { Key = "first", Order = 1 });
        session.Steps.Add(new WizardStep { Key = "second", Order = 2 });

        var flow = new WizardFlow(session);

        // Act
        var next = flow.GetNextStep();

        // Assert
        Assert.Null(next);
    }

    [Fact]
    public void GetPreviousStep_WithSavedData_ReturnsPrecedingStep()
    {
        // Arrange
        var session = new WizardSession
        {
            CurrentStep = "second",
        };
        session.Steps.Add(new WizardStep { Key = "first", Order = 1 });
        session.Steps.Add(new WizardStep { Key = "second", Order = 2 });
        session.SavedSteps["first"] = new System.Text.Json.Nodes.JsonObject();

        var flow = new WizardFlow(session);

        // Act
        var previous = flow.GetPreviousStep();

        // Assert
        Assert.Equal("first", previous.Key);
    }

    [Fact]
    public void GetPreviousStep_WithoutSavedData_ReturnsNull()
    {
        // Arrange
        var session = new WizardSession
        {
            CurrentStep = "second",
        };
        session.Steps.Add(new WizardStep { Key = "first", Order = 1 });
        session.Steps.Add(new WizardStep { Key = "second", Order = 2 });

        var flow = new WizardFlow(session);

        // Act
        var previous = flow.GetPreviousStep();

        // Assert
        Assert.Null(previous);
    }

    [Fact]
    public void SetCurrentStep_WithUnknownKey_Throws()
    {
        // Arrange
        var session = new WizardSession();
        session.Steps.Add(new WizardStep { Key = "first", Order = 1 });

        var flow = new WizardFlow(session);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => flow.SetCurrentStep("missing"));
    }
}
