using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

public sealed class SurveyDraftEditorTests
{
    private readonly SurveyDraftEditor _editor = new(AuthoringSurveyHarness.CreateRegistry(), new AuthoringLocalizer<SurveyDraftEditor>());

    [Fact]
    public void Apply_AddQuestion_UsesTheHandlerDefaultsAndAUniqueCode()
    {
        var definition = AuthoringDefinitions.TwoPages();

        var result = _editor.Apply(definition, new SurveyDraftCommand { Command = "addQuestion", PageId = "p1", ControlType = AuthoringSurveyHarness.ChoiceControl, Index = 0 });

        Assert.True(result.Succeeded);
        var added = definition.Pages[0].Questions[0];
        Assert.Equal(result.ItemId, added.Id);
        Assert.Equal(2, added.Choices.Count);
        Assert.Equal("q1", added.Name);
    }

    [Fact]
    public void Apply_AddQuestionFromPreset_UsesTheBaseTypeAndThePreset()
    {
        var definition = AuthoringDefinitions.TwoPages();

        _editor.Apply(definition, new SurveyDraftCommand { Command = "addQuestion", PageId = "p2", Preset = "TestPreset" });

        var added = definition.Pages[1].Questions.Last();
        Assert.Equal(AuthoringSurveyHarness.ChoiceControl, added.ControlType);
        Assert.Equal("preset", added.Placeholder);
    }

    [Fact]
    public void Apply_UnknownType_Fails()
        => Assert.False(_editor.Apply(AuthoringDefinitions.TwoPages(), new SurveyDraftCommand { Command = "addQuestion", ControlType = "Missing" }).Succeeded);

    [Fact]
    public void Apply_MoveQuestionToAnotherPage_MovesIt()
    {
        var definition = AuthoringDefinitions.TwoPages();

        _editor.Apply(definition, new SurveyDraftCommand { Command = "move", QuestionId = "q2", ToPageId = "p2", Index = 0 });

        Assert.Single(definition.Pages[0].Questions);
        Assert.Equal("q2", definition.Pages[1].Questions[0].Id);
    }

    [Fact]
    public void Apply_DuplicateQuestion_InsertsACopyWithNewIds()
    {
        var definition = AuthoringDefinitions.TwoPages();

        var result = _editor.Apply(definition, new SurveyDraftCommand { Command = "duplicate", QuestionId = "q1" });

        var copy = definition.Pages[0].Questions[1];
        Assert.Equal(result.ItemId, copy.Id);
        Assert.NotEqual("q1", copy.Id);
        Assert.DoesNotContain(copy.Choices, choice => choice.Id is "red" or "blue");
        Assert.NotEqual("color", copy.Name);
    }

    [Fact]
    public void Apply_DeleteLastPage_IsRefused()
    {
        var definition = AuthoringDefinitions.TwoPages();
        _editor.Apply(definition, new SurveyDraftCommand { Command = "deletePage", PageId = "p2" });

        var result = _editor.Apply(definition, new SurveyDraftCommand { Command = "deletePage", PageId = "p1" });

        Assert.False(result.Succeeded);
        Assert.Single(definition.Pages);
    }

    [Fact]
    public void Apply_DuplicatePage_CopiesItsQuestionsWithNewCodes()
    {
        var definition = AuthoringDefinitions.TwoPages();

        _editor.Apply(definition, new SurveyDraftCommand { Command = "duplicatePage", PageId = "p1" });

        Assert.Equal(3, definition.Pages.Count);
        var copy = definition.Pages[1];
        Assert.Equal(2, copy.Questions.Count);
        var names = definition.Pages.SelectMany(page => page.Questions).Select(question => question.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
