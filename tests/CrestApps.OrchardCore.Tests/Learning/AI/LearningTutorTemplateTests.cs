using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.Learning.AI;
using CrestApps.OrchardCore.Learning.AI.Handlers;
using CrestApps.OrchardCore.Learning.AI.Models;
using CrestApps.OrchardCore.Learning.AI.Services;

namespace CrestApps.OrchardCore.Tests.Learning.AI;

/// <summary>
/// The tutor profile template and the per-course switch: the template ships with the Learning tools and the rule that
/// it never gives answers, a profile built from it is a tutor, and a course's switch starts off.
/// </summary>
public sealed class LearningTutorTemplateTests
{
    [Fact]
    public void Template_RequiresTheFeatureAndPicksTheLearnerTools()
    {
        var text = File.ReadAllText(FindRepositoryFile(Path.Combine("src", "Modules", "CrestApps.OrchardCore.Learning.AI", "Templates", "Profiles", LearningAIConstants.Templates.Tutor + ".md")));
        var frontMatter = text.Split("---", 3, StringSplitOptions.None)[1];
        var values = frontMatter
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Contains(':', StringComparison.Ordinal))
            .ToDictionary(line => line[..line.IndexOf(':', StringComparison.Ordinal)], line => line[(line.IndexOf(':', StringComparison.Ordinal) + 1)..].Trim(), StringComparer.Ordinal);

        Assert.Equal(LearningAIConstants.Features.Assistant, values["RequiresFeatures"]);
        Assert.Equal("Chat", values["ProfileType"]);
        Assert.Equal(
            [LearningAIConstants.Tools.MyProgress, LearningAIConstants.Tools.MyNextLesson, LearningAIConstants.Tools.ExplainMyResult],
            values["ToolNames"].Split(',', StringSplitOptions.TrimEntries));
        Assert.Contains("Never give or hint at the answers", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileBuiltFromTheTutorTemplate_IsATutor_AndOtherTemplatesLeaveProfilesAlone()
    {
        var handler = new LearningTutorTemplateApplicationHandler();
        var tutor = new AIProfile { ItemId = "p1" };
        var other = new AIProfile { ItemId = "p2" };

        await handler.AppliedAsync(new AIProfileTemplateAppliedContext(tutor, new AIProfileTemplate { ItemId = "t1", Name = LearningAIConstants.Templates.Tutor }));
        await handler.AppliedAsync(new AIProfileTemplateAppliedContext(other, new AIProfileTemplate { ItemId = "t2", Name = "website-assistant" }));

        Assert.True(LearningTutorProfiles.IsTutor(tutor));
        Assert.False(LearningTutorProfiles.IsTutor(other));
        Assert.False(other.TryGet<LearningTutorProfileMetadata>(out _));
    }

    [Fact]
    public void CourseSwitch_IsOffByDefault()
    {
        Assert.False(new LearningAssistantPart().Enabled);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The file was not found above the test output folder.", relativePath);
    }
}
