using CrestApps.OrchardCore.Surveys.Core.Controls;
using OrchardCore.DisplayManagement.Descriptors.ShapeTemplateStrategy;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// The view names documented for a question type bind to the shape the runner asks for, with Orchard's own harvester,
/// for dotted names such as <c>Acme.Thumbs</c> as well as built-in names.
/// </summary>
public sealed class SurveyControlShapeNameTests
{
    [Theory]
    [InlineData("SurveyControl-Acme.Thumbs.Respond", "Respond", "Acme.Thumbs")]
    [InlineData("SurveyControl-Acme.Thumbs.Review", "Review", "Acme.Thumbs")]
    [InlineData("SurveyControl-Acme.Surveys.Thumbs.Respond", "Respond", "Acme.Surveys.Thumbs")]
    [InlineData("SurveyControl-Thumbs.Respond", "Respond", "Thumbs")]
    [InlineData("SurveyControl.Respond-Boolean", "Respond", "Boolean")]
    [InlineData("SurveyControl.Review-SingleChoice", "Review", "SingleChoice")]
    public void DocumentedViewName_BindsToTheRequestedAlternate(string fileName, string displayType, string controlType)
    {
        // Arrange
        var alternates = SurveyControlShapes.GetAlternates(displayType, controlType, surveyName: null, questionName: null);

        // Act
        var shapeType = Harvest(fileName);

        // Assert
        Assert.Contains(alternates, alternate => string.Equals(alternate, shapeType, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DocumentedViewName_ForASurveyOverride_BindsToTheSurveyAlternate()
    {
        // Arrange
        var alternates = SurveyControlShapes.GetAlternates("Respond", "Acme.Thumbs", "customer-feedback", questionName: null);

        // Act
        var shapeType = Harvest("SurveyControl-Acme.Thumbs-customer_feedback.Respond");

        // Assert
        Assert.Contains(alternates, alternate => string.Equals(alternate, shapeType, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DocumentedSettingsViewName_ForADottedName_BindsToTheSettingsEditor()
    {
        // Arrange
        var requested = $"{SurveyControlShapes.Settings}_Edit__{SurveyControlShapes.Encode("Acme.Thumbs")}";

        // Act
        var shapeType = Harvest("SurveyControlSettings-Acme.Thumbs.Edit");

        // Assert
        Assert.Equal(requested, shapeType, ignoreCase: true);
    }

    [Fact]
    public void DisplayTypeFirstViewName_ForADottedName_DoesNotBind()
    {
        // The form the built-in views use reads the last dotted segment of the name as the display type, which is
        // why the documented form puts the display type last.
        var alternates = SurveyControlShapes.GetAlternates("Respond", "Acme.Thumbs", surveyName: null, questionName: null);

        var shapeType = Harvest("SurveyControl.Respond-Acme.Thumbs");

        Assert.DoesNotContain(alternates, alternate => string.Equals(alternate, shapeType, StringComparison.OrdinalIgnoreCase));
    }

    private static string Harvest(string fileName)
    {
        var hits = new BasicShapeTemplateHarvester().HarvestShape(new HarvestShapeInfo
        {
            SubPath = "Views",
            FileName = fileName,
        }).ToList();

        return Assert.Single(hits).ShapeType;
    }
}
