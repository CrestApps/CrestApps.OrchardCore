using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyDefinitionDifferTests
{
    private readonly SurveyDefinitionDiffer _differ = new();

    [Fact]
    public void Compare_SameDefinition_HasNoChanges()
        => Assert.Empty(_differ.Compare(AuthoringDefinitions.TwoPages(), AuthoringDefinitions.TwoPages()).Changes);

    [Fact]
    public void Compare_RemovedQuestion_IsDestructive()
    {
        var after = AuthoringDefinitions.TwoPages();
        after.Pages[0].Questions.RemoveAt(1);

        var diff = _differ.Compare(AuthoringDefinitions.TwoPages(), after);

        Assert.True(diff.HasDestructiveChanges);
        Assert.Contains(diff.Changes, change => change.Kind == SurveyChangeKind.Removed && change.Id == "q2");
    }

    [Fact]
    public void Compare_ControlTypeChanged_IsDestructive()
    {
        var after = AuthoringDefinitions.TwoPages();
        after.Pages[0].Questions[1].ControlType = AuthoringSurveyHarness.ChoiceControl;

        Assert.Contains(_differ.Compare(AuthoringDefinitions.TwoPages(), after).Changes, change => change.Detail == "controlType" && change.Destructive);
    }

    [Fact]
    public void Compare_RetiredChoice_IsDestructive()
    {
        var after = AuthoringDefinitions.TwoPages();
        after.Pages[0].Questions[0].Choices[0].Retired = true;

        Assert.Contains(_differ.Compare(AuthoringDefinitions.TwoPages(), after).Changes, change => change.Target == SurveyDefinitionDiffer.ChoiceTarget && change.Detail == "retired" && change.Destructive);
    }

    [Fact]
    public void Compare_ScoringKeyChanged_IsDestructive()
    {
        var after = AuthoringDefinitions.TwoPages();
        after.Pages[0].Questions[0].Scoring.CorrectChoiceIds = ["red"];

        Assert.Contains(_differ.Compare(AuthoringDefinitions.TwoPages(), after).Changes, change => change.Detail == "scoring" && change.Destructive);
    }

    [Fact]
    public void Compare_RequiredAdded_IsDestructive()
    {
        var after = AuthoringDefinitions.TwoPages();
        after.Pages[1].Questions[0].Required = true;

        Assert.Contains(_differ.Compare(AuthoringDefinitions.TwoPages(), after).Changes, change => change.Detail == "required" && change.Destructive);
    }

    [Fact]
    public void Compare_TitleChangedAndQuestionAdded_AreNotDestructive()
    {
        var after = AuthoringDefinitions.TwoPages();
        after.Pages[0].Questions[1].Title = "Comments";
        after.Pages[1].Questions.Add(new SurveyQuestion { Id = "q4", Name = "extra", ControlType = AuthoringSurveyHarness.TextControl });

        var diff = _differ.Compare(AuthoringDefinitions.TwoPages(), after);

        Assert.False(diff.HasDestructiveChanges);
        Assert.Contains(diff.Changes, change => change.Kind == SurveyChangeKind.Added && change.Id == "q4");
        Assert.Contains(diff.Changes, change => change.Kind == SurveyChangeKind.Changed && change.Id == "q2");
    }

    [Fact]
    public void Compare_QuestionMovedToAnotherPage_IsMoved()
    {
        var after = AuthoringDefinitions.TwoPages();
        var question = after.Pages[0].Questions[1];
        after.Pages[0].Questions.Remove(question);
        after.Pages[1].Questions.Add(question);

        Assert.Contains(_differ.Compare(AuthoringDefinitions.TwoPages(), after).Changes, change => change.Kind == SurveyChangeKind.Moved && change.Id == "q2");
    }
}
