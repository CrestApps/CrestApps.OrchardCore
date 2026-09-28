using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// What an automated call or message is told about the dispositions it must choose between.
/// </summary>
/// <remarks>
/// The model picks a disposition from a list of names and descriptions, so the description is the whole of what
/// it has to go on. These tests hold the rule that decides which description that is.
/// </remarks>
public class SubjectDispositionGuidanceTests
{
    [Fact]
    public void ADisposition_IsDescribedByItsOwnDescription_WhenTheSubjectSaysNothingMore()
    {
        // Arrange
        var dispositions = new[] { Disposition("d1", "Do Not Call", "The customer asked not to be contacted again.") };

        // Act
        var described = SubjectDispositionGuidance.Describe(dispositions, [], "Opportunity");

        // Assert
        Assert.Equal("The customer asked not to be contacted again.", Assert.Single(described).Description);
    }

    [Fact]
    public void TheSubjectsOwnWording_ReplacesTheGeneralDescription()
    {
        // Arrange
        // The same disposition means different things on different work, and the subject-specific wording is the
        // one written with this workflow in mind.
        var dispositions = new[] { Disposition("d1", "Done", "The call is finished.") };
        var actions = new[] { Action("Opportunity", "d1", "Only when the customer booked a test drive.") };

        // Act
        var described = SubjectDispositionGuidance.Describe(dispositions, actions, "Opportunity");

        // Assert
        Assert.Equal("Only when the customer booked a test drive.", Assert.Single(described).Description);
    }

    [Fact]
    public void GuidanceFromAnotherSubject_IsNotUsed()
    {
        // Arrange
        // Actions are configured per subject type and read from one catalog, so the filter is what stops a
        // service workflow's wording describing a sales call.
        var dispositions = new[] { Disposition("d1", "Done", "The call is finished.") };
        var actions = new[] { Action("ServiceTicket", "d1", "Only when the vehicle was collected.") };

        // Act
        var described = SubjectDispositionGuidance.Describe(dispositions, actions, "Opportunity");

        // Assert
        Assert.Equal("The call is finished.", Assert.Single(described).Description);
    }

    [Fact]
    public void BlankGuidance_LeavesTheGeneralDescriptionInPlace()
    {
        // Arrange
        // An empty box is not an instruction to tell the model nothing.
        var dispositions = new[] { Disposition("d1", "Done", "The call is finished.") };
        var actions = new[] { Action("Opportunity", "d1", "   ") };

        // Act
        var described = SubjectDispositionGuidance.Describe(dispositions, actions, "Opportunity");

        // Assert
        Assert.Equal("The call is finished.", Assert.Single(described).Description);
    }

    [Fact]
    public void EveryDisposition_IsOfferedWithTheIdTheModelMustReturn()
    {
        // Arrange
        var dispositions = new[]
        {
            Disposition("d1", "Done", "The call is finished."),
            Disposition("d2", "No Answer", "Nobody picked up."),
        };

        // Act
        var described = SubjectDispositionGuidance.Describe(dispositions, [], "Opportunity");

        // Assert
        Assert.Equal(["d1", "d2"], described.Select(choice => choice.Id));
        Assert.Equal(["Done", "No Answer"], described.Select(choice => choice.Name));
    }

    private static OmnichannelDisposition Disposition(string id, string name, string description)
        => new() { ItemId = id, Name = name, Description = description };

    private static SubjectAction Action(string subjectContentType, string dispositionId, string guidance)
        => new()
        {
            SubjectContentType = subjectContentType,
            DispositionId = dispositionId,
            DispositionGuidance = guidance,
        };
}
