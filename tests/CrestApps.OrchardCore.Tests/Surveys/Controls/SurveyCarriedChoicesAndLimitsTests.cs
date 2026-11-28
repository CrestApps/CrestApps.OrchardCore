using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.Date;
using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;
using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Core.Formulas;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using static CrestApps.OrchardCore.Tests.Surveys.TestSupport.ControlsTestData;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// Round 12: a Date question compares only the part of Earliest and Latest its mode asks for (M6), a required Ranking
/// question counts only the items the respondent is shown (M7), and a question with carried-forward choices reads its
/// choices from the question it carries them from wherever it formats, exports, indexes or computes (M8).
/// </summary>
public sealed class SurveyCarriedChoicesAndLimitsTests
{
    private static T Handler<T>()
        where T : ISurveyControlHandler
        => ControlsTestServices.Build().CreateScope().ServiceProvider.GetServices<ISurveyControlHandler>().OfType<T>().Single();

    [Fact]
    public async Task Date_TimeMode_ComparesOnlyTheTimeOfEarliestAndLatest()
    {
        // Arrange: Earliest and Latest were entered with a date, as the designer's date-and-time editor posts them.
        var question = Question(SurveyControlNames.Date);
        question.SetSettings(new DateSettings { Mode = DateMode.Time, Min = new DateTime(2026, 11, 27, 9, 0, 0), Max = new DateTime(2026, 11, 27, 17, 0, 0) });
        var inside = Read(question, Form(("q.q1", ["10:00"])));
        var late = Read(question, Form(("q.q1", ["18:00"])));
        var handler = Handler<DateControlHandler>();
        await handler.ReadAnswerAsync(inside);
        await handler.ReadAnswerAsync(late);
        var insideCheck = Validate(question, inside.Answer);
        var lateCheck = Validate(question, late.Answer);

        // Act
        await handler.ValidateAnswerAsync(insideCheck);
        await handler.ValidateAnswerAsync(lateCheck);

        // Assert
        Assert.Empty(insideCheck.Errors);
        Assert.Single(lateCheck.Errors);
    }

    [Fact]
    public async Task Date_DateMode_AcceptsTheEarliestDayWhateverTimeEarliestCarries()
    {
        // Arrange
        var question = Question(SurveyControlNames.Date);
        question.SetSettings(new DateSettings { Mode = DateMode.Date, Min = new DateTime(2026, 11, 27, 10, 30, 0), Max = new DateTime(2026, 11, 30, 8, 0, 0) });
        var first = Validate(question, new SurveyAnswer { Date = new DateTime(2026, 11, 27) });
        var last = Validate(question, new SurveyAnswer { Date = new DateTime(2026, 11, 30) });
        var after = Validate(question, new SurveyAnswer { Date = new DateTime(2026, 12, 1) });
        var handler = Handler<DateControlHandler>();

        // Act
        await handler.ValidateAnswerAsync(first);
        await handler.ValidateAnswerAsync(last);
        await handler.ValidateAnswerAsync(after);

        // Assert
        Assert.Empty(first.Errors);
        Assert.Empty(last.Errors);
        Assert.Single(after.Errors);
    }

    [Fact]
    public async Task Ranking_Required_CountsOnlyTheItemsShown()
    {
        // Arrange: a rule hides item e, so the page shows four of the five items.
        var question = Question(SurveyControlNames.Ranking, ("a", "A"), ("b", "B"), ("c", "C"), ("d", "D"), ("e", "E"));
        var context = Validate(question, new SurveyAnswer { Values = ["d", "c", "b", "a"] }, required: true);
        context.AllowedChoiceIds = ["a", "b", "c", "d"];

        // Act
        await Handler<RankingControlHandler>().ValidateAnswerAsync(context);

        // Assert
        Assert.Empty(context.Errors);
    }

    [Fact]
    public async Task Ranking_ReadAnswer_DropsAnItemThatIsNotShown()
    {
        // Arrange
        var question = Question(SurveyControlNames.Ranking, ("a", "A"), ("b", "B"), ("c", "C"));
        var ordered = Read(question, Form(("q.q1", ["c", "a", "b"])));
        ordered.AllowedChoiceIds = ["a", "c"];
        var ranks = Read(question, Form(("q.q1.a", ["2"]), ("q.q1.b", ["3"]), ("q.q1.c", ["1"])));
        ranks.AllowedChoiceIds = ["a", "c"];
        var handler = Handler<RankingControlHandler>();

        // Act
        await handler.ReadAnswerAsync(ordered);
        await handler.ReadAnswerAsync(ranks);

        // Assert
        Assert.Equal(["c", "a"], ordered.Answer.Values);
        Assert.Equal(["c", "a"], ranks.Answer.Values);
    }

    [Fact]
    public void CarriedForward_FormatAnswer_ShowsTheSourceChoiceTextAndTheOtherText()
    {
        // Arrange
        var (definition, _, carried) = Carried(SurveyControlNames.MultipleChoice);
        var context = new SurveyFormatContext { Definition = definition };
        var handler = Handler<MultipleChoiceControlHandler>();

        // Act
        var text = handler.FormatAnswer(carried, new SurveyAnswer { Values = ["b", "o"], OtherText = "Mine" }, context);
        var values = handler.FormatAnswer(carried, new SurveyAnswer { Values = ["b"] }, new SurveyFormatContext { Definition = definition, UseValues = true });

        // Assert
        Assert.Equal("B, Other: Mine", text);
        Assert.Equal("2", values);
    }

    [Fact]
    public void CarriedForward_Ranking_FormatAnswer_ShowsTheSourceChoiceTexts()
    {
        // Arrange
        var (definition, _, carried) = Carried(SurveyControlNames.Ranking);

        // Act
        var text = Handler<RankingControlHandler>().FormatAnswer(carried, new SurveyAnswer { Values = ["b", "a"] }, new SurveyFormatContext { Definition = definition });

        // Assert
        Assert.Equal("1. B, 2. A", text);
    }

    [Fact]
    public async Task CarriedForward_MultipleChoice_AnExclusiveSourceChoiceSkipsTheFewestChoices()
    {
        // Arrange
        var (definition, _, carried) = Carried(SurveyControlNames.MultipleChoice);
        carried.SetSettings(new MultipleChoiceSettings { MinSelected = 2 });
        var context = new SurveyAnswerValidationContext
        {
            Question = carried,
            Answer = new SurveyAnswer { Values = ["none"] },
            Definition = definition,
            Response = new SurveyResponse { ItemId = "r1" },
        };

        // Act
        await Handler<MultipleChoiceControlHandler>().ValidateAnswerAsync(context);

        // Assert
        Assert.Empty(context.Errors);
    }

    [Fact]
    public void CarriedForward_ExportColumns_WriteTheSourceChoiceTextAndAColumnPerSourceChoice()
    {
        // Arrange
        var (definition, _, _) = Carried(SurveyControlNames.MultipleChoice);
        var services = ControlsTestServices.Build().CreateScope().ServiceProvider;
        var version = new SurveyVersion { ItemId = "v1", SurveyId = "s1", Number = 1, Definition = definition };
        var columns = SurveyResponseColumns.Build([version], ControlsTestServices.Registry(services));
        var response = new SurveyResponse { ItemId = "r1", SurveyVersionId = "v1", Answers = { ["q2"] = new SurveyAnswer { Values = ["b"] } } };
        var row = new Dictionary<string, object>();

        // Act
        columns.Write(response, definition, row, new SurveyFormatContext());

        // Assert
        Assert.Equal("B", row["q_two"]);
        Assert.Equal(true, row["q_two.2"]);
        Assert.Equal(false, row["q_two.1"]);
    }

    [Fact]
    public void CarriedForward_IndexRows_KeepTheOtherText()
    {
        // Arrange
        var (definition, _, _) = Carried(SurveyControlNames.MultipleChoice);
        var services = ControlsTestServices.Build().CreateScope().ServiceProvider;
        var response = new SurveyResponse
        {
            ItemId = "r1",
            SurveyId = "s1",
            CompletedUtc = new DateTime(2026, 11, 27, 0, 0, 0, DateTimeKind.Utc),
            Answers = { ["q2"] = new SurveyAnswer { Values = ["o"], OtherText = "Mine" } },
        };

        // Act
        var rows = SurveyAnswerIndexRows.Build(response, definition, ControlsTestServices.Registry(services));

        // Assert
        Assert.Equal("Mine", Assert.Single(rows, row => row.ChoiceId == "o").TextValue);
    }

    [Fact]
    public async Task CarriedForward_Formula_SelectedReadsTheSourceChoiceValue()
    {
        // Arrange
        await using var harness = await RuntimeTestHarness.CreateAsync(services =>
        {
            services.AddSurveyControl<SingleChoiceControlHandler>(SurveyControlNames.SingleChoice, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.ConditionSource);
            services.AddSurveyControl<MultipleChoiceControlHandler>(SurveyControlNames.MultipleChoice, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.ConditionSource);
        });
        var (definition, _, _) = Carried(SurveyControlNames.MultipleChoice);
        var response = new SurveyResponse { Answers = { ["q2"] = new SurveyAnswer { Values = ["b"] } } };

        // Act
        var selected = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<SurveyFormulaService>().EvaluateBoolean(definition, response, "SELECTED([q_two], '2')")));

        // Assert
        Assert.True(selected);
    }

    // q1 offers A, B, Other and None (exclusive); q2 carries every choice of q1 forward and has none of its own.
    private static (SurveyDefinition Definition, SurveyQuestion Source, SurveyQuestion Carried) Carried(string controlType)
    {
        var source = new SurveyQuestion
        {
            Id = "q1",
            Name = "q_one",
            Title = "Question one",
            ControlType = SurveyControlNames.MultipleChoice,
            Choices =
            [
                new SurveyChoice { Id = "a", Text = "A", Value = "1" },
                new SurveyChoice { Id = "b", Text = "B", Value = "2" },
                new SurveyChoice { Id = "o", Text = "Other", Value = "3", IsOther = true },
                new SurveyChoice { Id = "none", Text = "None", Value = "4", IsExclusive = true },
            ],
        };
        var carried = new SurveyQuestion
        {
            Id = "q2",
            Name = "q_two",
            Title = "Question two",
            ControlType = controlType,
            ChoicesFrom = "q1",
            ChoicesFromMode = ChoicesFromMode.All,
            Choices = [],
        };
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [source, carried] }] };

        return (definition, source, carried);
    }
}
