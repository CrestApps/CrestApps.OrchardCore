using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.Boolean;
using CrestApps.OrchardCore.Surveys.Core.Controls.Calculated;
using CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;
using CrestApps.OrchardCore.Surveys.Core.Controls.ContactInfo;
using CrestApps.OrchardCore.Surveys.Core.Controls.Date;
using CrestApps.OrchardCore.Surveys.Core.Controls.FileUpload;
using CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;
using CrestApps.OrchardCore.Surveys.Core.Controls.Hidden;
using CrestApps.OrchardCore.Surveys.Core.Controls.Matrix;
using CrestApps.OrchardCore.Surveys.Core.Controls.MultipleChoice;
using CrestApps.OrchardCore.Surveys.Core.Controls.Number;
using CrestApps.OrchardCore.Surveys.Core.Controls.Ranking;
using CrestApps.OrchardCore.Surveys.Core.Controls.Rating;
using CrestApps.OrchardCore.Surveys.Core.Controls.SingleChoice;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;
using static CrestApps.OrchardCore.Tests.Surveys.TestSupport.ControlsTestData;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// The built-in question types read, check, compare, format, export and index their answers.
/// </summary>
public sealed class SurveyControlHandlerTests
{
    private static T Handler<T>()
        where T : ISurveyControlHandler
    {
        var services = ControlsTestServices.Build().CreateScope().ServiceProvider;

        return services.GetServices<ISurveyControlHandler>().OfType<T>().Single();
    }

    [Fact]
    public async Task SingleChoice_ReadAnswer_KeepsOnlyAnAllowedChoiceAndTheOtherText()
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "A"), ("o", "Other"));
        question.Choices[1].IsOther = true;
        var context = Read(question, Form(("q.q1", ["o"]), ("q.q1.other", ["Mine"])));

        await Handler<SingleChoiceControlHandler>().ReadAnswerAsync(context);

        Assert.Equal(["o"], context.Answer.Values);
        Assert.Equal("Mine", context.Answer.OtherText);
    }

    [Fact]
    public async Task SingleChoice_ReadAnswer_DropsAChoiceThatIsNotAllowed()
    {
        var question = Question(SurveyControlNames.SingleChoice, ("a", "A"), ("b", "B"));
        var context = Read(question, Form(("q.q1", ["zzz"])));

        await Handler<SingleChoiceControlHandler>().ReadAnswerAsync(context);

        Assert.Null(context.Answer);
    }

    [Fact]
    public async Task MultipleChoice_ReadAnswer_ExclusiveChoiceClearsTheOthers()
    {
        var question = Question(SurveyControlNames.MultipleChoice, ("a", "A"), ("b", "B"), ("n", "None"));
        question.Choices[2].IsExclusive = true;
        var context = Read(question, Form(("q.q1", ["a", "n"])));

        await Handler<MultipleChoiceControlHandler>().ReadAnswerAsync(context);

        Assert.Equal(["n"], context.Answer.Values);
    }

    [Fact]
    public async Task MultipleChoice_ValidateAnswer_EnforcesTheFewestAndMostChoices()
    {
        var handler = Handler<MultipleChoiceControlHandler>();
        var question = Question(SurveyControlNames.MultipleChoice, ("a", "A"), ("b", "B"), ("c", "C"));
        question.SetSettings(new MultipleChoiceSettings { MinSelected = 2, MaxSelected = 2 });

        var tooFew = Validate(question, new SurveyAnswer { Values = ["a"] });
        var right = Validate(question, new SurveyAnswer { Values = ["a", "b"] });
        await handler.ValidateAnswerAsync(tooFew);
        await handler.ValidateAnswerAsync(right);

        Assert.Single(tooFew.Errors);
        Assert.Empty(right.Errors);
    }

    [Fact]
    public async Task Required_ValidateAnswer_RefusesAnEmptyAnswer()
    {
        var context = Validate(Question(SurveyControlNames.ShortText), null, required: true);

        await Handler<CrestApps.OrchardCore.Surveys.Core.Controls.ShortText.ShortTextControlHandler>().ValidateAnswerAsync(context);

        Assert.Single(context.Errors);
    }

    [Fact]
    public async Task Boolean_ReadAnswer_ToggleReadsTheLastPostedValue()
    {
        var question = Question(SurveyControlNames.Boolean);
        question.SetSettings(new BooleanSettings { Display = BooleanDisplay.Toggle });
        var on = Read(question, Form(("q.q1", [BooleanValues.False, BooleanValues.True])));
        var off = Read(question, Form(("q.q1", [BooleanValues.False])));
        var handler = Handler<BooleanControlHandler>();

        await handler.ReadAnswerAsync(on);
        await handler.ReadAnswerAsync(off);

        Assert.Equal([BooleanValues.True], on.Answer.Values);
        Assert.Equal([BooleanValues.False], off.Answer.Values);
        Assert.Equal(true, handler.GetFormulaValue(question, on.Answer, null));
        Assert.Equal(DataFieldType.Boolean, handler.GetFormulaType(question, null));
    }

    [Fact]
    public async Task Number_ReadAnswer_ReportsTextThatIsNotANumber()
    {
        var context = Read(Question(SurveyControlNames.Number), Form(("q.q1", ["abc"])));

        await Handler<NumberControlHandler>().ReadAnswerAsync(context);

        Assert.Null(context.Answer);
        Assert.Single(context.Errors);
    }

    [Fact]
    public async Task Number_ValidateAnswer_EnforcesTheRange()
    {
        var question = Question(SurveyControlNames.Number);
        question.SetSettings(new NumberSettings { Min = 1, Max = 10 });
        var context = Validate(question, new SurveyAnswer { Number = 11 });

        await Handler<NumberControlHandler>().ValidateAnswerAsync(context);

        Assert.Single(context.Errors);
    }

    [Fact]
    public async Task Date_ReadAnswer_KeepsTheWallClockValueEntered()
    {
        var question = Question(SurveyControlNames.Date);
        question.SetSettings(new DateSettings { Mode = DateMode.DateTime });
        var context = Read(question, Form(("q.q1", ["2026-03-08T02:30"])));

        await Handler<DateControlHandler>().ReadAnswerAsync(context);

        Assert.Equal(new DateTime(2026, 3, 8, 2, 30, 0), context.Answer.Date);
        Assert.Equal(DateTimeKind.Unspecified, context.Answer.Date.Value.Kind);
    }

    [Fact]
    public async Task Rating_ReadAnswer_RefusesAValueOutsideTheScale()
    {
        var question = Question(SurveyControlNames.Rating);
        question.SetSettings(new RatingSettings { Min = 1, Max = 5 });
        var inside = Read(question, Form(("q.q1", ["4"])));
        var outside = Read(question, Form(("q.q1", ["9"])));
        var handler = Handler<RatingControlHandler>();

        await handler.ReadAnswerAsync(inside);
        await handler.ReadAnswerAsync(outside);

        Assert.Equal(4, inside.Answer.Number);
        Assert.Null(outside.Answer);
        Assert.Equal(4L, handler.GetFormulaValue(question, inside.Answer, null));
    }

    [Fact]
    public async Task Matrix_ReadAnswer_ReadsEachRowAndConditionsReadARow()
    {
        var question = Question(SurveyControlNames.Matrix, ("good", "Good"), ("bad", "Bad"));
        question.Rows = [new SurveySubQuestion { Id = "r1", Name = "speed", Text = "Speed" }, new SurveySubQuestion { Id = "r2", Name = "price", Text = "Price" }];
        var context = Read(question, Form(("q.q1.r1", ["good"]), ("q.q1.r2", ["bad"])));
        var handler = Handler<MatrixControlHandler>();

        await handler.ReadAnswerAsync(context);

        Assert.Equal(["good"], context.Answer.Rows["r1"]);
        Assert.Equal(["bad"], handler.GetConditionValue(question, context.Answer, "r2").Values);
        Assert.Equal(2, handler.GetIndexRows(question, context.Answer).Count());
        Assert.Equal(["q_one.speed", "q_one.price"], handler.DescribeColumns(question).Select(column => column.Key));
    }

    [Fact]
    public async Task Ranking_ReadAnswer_ReadsTheOrderedListOrTheRanks()
    {
        var question = Question(SurveyControlNames.Ranking, ("a", "A"), ("b", "B"), ("c", "C"));
        var ordered = Read(question, Form(("q.q1", ["c", "a", "b"])));
        var ranks = Read(question, Form(("q.q1.a", ["2"]), ("q.q1.b", ["3"]), ("q.q1.c", ["1"])));
        var handler = Handler<RankingControlHandler>();

        await handler.ReadAnswerAsync(ordered);
        await handler.ReadAnswerAsync(ranks);

        Assert.Equal(["c", "a", "b"], ordered.Answer.Values);
        Assert.Equal(["c", "a", "b"], ranks.Answer.Values);
        Assert.Equal([1m, 2m, 3m], handler.GetIndexRows(question, ordered.Answer).Select(row => row.NumberValue.Value));
    }

    [Fact]
    public async Task FillInBlanks_ReadAnswer_KeepsAValuePerBlank()
    {
        var question = Question(SurveyControlNames.FillInBlanks);
        question.SetSettings(new FillInBlanksSettings { Text = "A [[x]] and a [[y]]." });
        var context = Read(question, Form(("q.q1.0", [""]), ("q.q1.1", ["y"])));
        var handler = Handler<FillInBlanksControlHandler>();

        await handler.ReadAnswerAsync(context);
        var validation = Validate(question, context.Answer, required: true);
        await handler.ValidateAnswerAsync(validation);

        Assert.Equal(["", "y"], context.Answer.Values);
        Assert.Single(validation.Errors);
    }

    [Fact]
    public async Task ConstantSum_ValidateAnswer_RequiresTheTotal()
    {
        var question = Question(SurveyControlNames.ConstantSum);
        question.Rows = [new SurveySubQuestion { Id = "r1", Text = "A" }, new SurveySubQuestion { Id = "r2", Text = "B" }];
        question.SetSettings(new ConstantSumSettings { Total = 100 });
        var context = Read(question, Form(("q.q1.r1", ["60"]), ("q.q1.r2", ["30"])));
        var handler = Handler<ConstantSumControlHandler>();

        await handler.ReadAnswerAsync(context);
        var validation = Validate(question, context.Answer);
        await handler.ValidateAnswerAsync(validation);

        Assert.Single(validation.Errors);
        Assert.Equal(60m, handler.GetFormulaValue(question, context.Answer, "r1"));
    }

    [Fact]
    public async Task ContactInfo_ValidateAnswer_ChecksRequiredFieldsAndTheEmail()
    {
        var question = Question(SurveyControlNames.ContactInfo);
        var context = Read(question, Form(("q.q1.name", ["Ada"]), ("q.q1.email", ["not-an-email"])));
        var handler = Handler<ContactInfoControlHandler>();

        await handler.ReadAnswerAsync(context);
        var validation = Validate(question, context.Answer, required: true);
        await handler.ValidateAnswerAsync(validation);

        Assert.Contains("Enter a valid e-mail address.", validation.Errors);
        Assert.Empty(handler.GetIndexRows(question, context.Answer));
    }

    [Fact]
    public async Task Hidden_ReadAnswer_RecordsTheVariable()
    {
        var question = Question(SurveyControlNames.Hidden);
        question.SetSettings(new HiddenSettings { VariableName = "source" });
        var response = new SurveyResponse { ItemId = "r1", Variables = new() { ["source"] = "newsletter" } };
        var context = Read(question, Form(("q.q1", ["tampered"])), response);

        await Handler<HiddenControlHandler>().ReadAnswerAsync(context);

        Assert.Equal(["newsletter"], context.Answer.Values);
    }

    [Fact]
    public async Task Calculated_ReadAnswer_RecordsTheValueTheRuntimeCalculated()
    {
        var question = Question(SurveyControlNames.Calculated);
        question.SetSettings(new CalculatedSettings { Formula = "[a] * 2" });
        var response = new SurveyResponse { ItemId = "r1", Variables = new() { ["q_one"] = "42.5" } };
        var context = Read(question, Form(), response);
        var handler = Handler<CalculatedControlHandler>();

        await handler.ReadAnswerAsync(context);

        Assert.Equal(42.5m, context.Answer.Number);
        Assert.Equal("[a] * 2", ((ISurveyCalculatedControl)handler).GetFormula(question));
    }

    [Fact]
    public async Task FileUpload_ReadAnswer_KeepsOnlyFilesUploadedBefore()
    {
        var question = Question(SurveyControlNames.FileUpload);
        var existing = new SurveyAnswer { AttachmentIds = ["f1", "f2"] };
        var context = Read(question, Form(("q.q1.keep", ["f2", "stranger"])), existing: existing);

        await Handler<FileUploadControlHandler>().ReadAnswerAsync(context);

        Assert.Equal(["f2"], context.Answer.AttachmentIds);
    }

    [Fact]
    public async Task ReadAnswer_FromJson_ReadsArrays()
    {
        var question = Question(SurveyControlNames.MultipleChoice, ("a", "A"), ("b", "B"));
        var context = ReadJson(question, new JsonArray("a", "b"));

        await Handler<MultipleChoiceControlHandler>().ReadAnswerAsync(context);

        Assert.Equal(["a", "b"], context.Answer.Values);
    }

    [Fact]
    public void MultipleChoice_WriteColumns_WritesTheAnswerAndAColumnPerChoice()
    {
        var handler = Handler<MultipleChoiceControlHandler>();
        var question = Question(SurveyControlNames.MultipleChoice, ("a", "Apple"), ("b", "Banana"));
        var row = new Dictionary<string, object>();

        handler.WriteColumns(question, new SurveyAnswer { Values = ["b"] }, row, new SurveyFormatContext());

        Assert.Equal("Banana", row["q_one"]);
        Assert.Equal(false, row["q_one.1"]);
        Assert.Equal(true, row["q_one.2"]);
    }

    [Fact]
    public void SingleChoice_FormatAnswer_WritesValuesForExportsAndTextsOtherwise()
    {
        var handler = Handler<SingleChoiceControlHandler>();
        var question = Question(SurveyControlNames.SingleChoice, ("a", "Apple"));
        var answer = new SurveyAnswer { Values = ["a"] };

        Assert.Equal("Apple", handler.FormatAnswer(question, answer, new SurveyFormatContext()));
        Assert.Equal("1", handler.FormatAnswer(question, answer, new SurveyFormatContext { UseValues = true }));
        Assert.Equal("1", handler.GetFormulaValue(question, answer, null));
        Assert.Equal(["a"], handler.GetConditionValue(question, answer, null).Values);
        Assert.Equal("a", handler.GetIndexRows(question, answer).Single().ChoiceId);
    }

    [Fact]
    public void OrderChoices_Shuffle_KeepsPinnedChoicesInPlaceAndOtherLast()
    {
        var choices = new List<SurveyChoice>
        {
            new() { Id = "a", Pinned = true },
            new() { Id = "b" },
            new() { Id = "c" },
            new() { Id = "d" },
            new() { Id = "x", Retired = true },
            new() { Id = "o", IsOther = true },
        };

        var first = SurveyChoiceSource.OrderChoices(choices, shuffle: true, new SurveyRandom(7));
        var second = SurveyChoiceSource.OrderChoices(choices, shuffle: true, new SurveyRandom(7));

        Assert.Equal("a", first[0]);
        Assert.Equal("o", first[first.Count - 1]);
        Assert.DoesNotContain("x", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Validation_Regex_RefusesATextThatDoesNotMatch()
    {
        var question = Question(SurveyControlNames.ShortText);
        question.Validation = [new SurveyValidationRule { Kind = SurveyValidationKind.Regex, Value = "^[0-9]{3}$" }];
        var context = Validate(question, new SurveyAnswer { Values = ["12a"] });

        await Handler<CrestApps.OrchardCore.Surveys.Core.Controls.ShortText.ShortTextControlHandler>().ValidateAnswerAsync(context);

        Assert.Single(context.Errors);
    }
}
