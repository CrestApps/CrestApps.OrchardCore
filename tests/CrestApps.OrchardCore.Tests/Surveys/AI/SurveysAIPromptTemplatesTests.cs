using CrestApps.OrchardCore.Surveys.AI;

namespace CrestApps.OrchardCore.Tests.Surveys.AI;

public sealed class SurveysAIPromptTemplatesTests
{
    [Theory]
    [InlineData(SurveysAIConstants.Templates.ScoreAnswer)]
    [InlineData(SurveysAIConstants.Templates.DraftSurvey)]
    [InlineData(SurveysAIConstants.Templates.SummarizeAnswers)]
    [InlineData(SurveysAIConstants.Templates.Translate)]
    public void EveryTemplateId_HasAPromptFileWithFrontMatter(string templateId)
    {
        // Arrange
        var path = Path.Combine(RepositoryRoot(), "src", "Modules", "CrestApps.OrchardCore.Surveys.AI", "Templates", "Prompts", templateId + ".md");

        // Act
        var text = File.ReadAllText(path);

        // Assert
        Assert.StartsWith("---", text, StringComparison.Ordinal);
        Assert.Contains("\nTitle: ", text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
