using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.ConstantSum;
using CrestApps.OrchardCore.Surveys.Core.Controls.Number;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using static CrestApps.OrchardCore.Tests.Surveys.TestSupport.ControlsTestData;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// Numbers near the decimal limits make an answer wrong or invalid, never a failed request.
/// </summary>
public sealed class SurveyNumberOverflowTests
{
    [Fact]
    public void Score_ANumberNearTheDecimalLimits_IsWrong()
    {
        // Arrange
        var question = Question(SurveyControlNames.Number);
        question.Scoring = new SurveyQuestionScoring { NumberAnswer = -1m, Tolerance = 0.5m };

        // Act
        var result = SurveyNumberScoring.Score(question, new SurveyAnswer { Number = decimal.MaxValue }, 1, 0);

        // Assert
        Assert.False(result.Correct);
        Assert.False(SurveyNumberScoring.IsWithin(decimal.MinValue, decimal.MaxValue, decimal.MaxValue));
        Assert.True(SurveyNumberScoring.IsWithin(10m, 10.25m, 0.5m));
    }

    [Fact]
    public async Task Number_ValidateAnswer_ANumberFarOffTheSteps_IsInvalid()
    {
        // Arrange
        var question = Question(SurveyControlNames.Number);
        question.SetSettings(new NumberSettings { Min = -10, Step = 2, Decimals = 40 });
        var context = Validate(question, new SurveyAnswer { Number = decimal.MaxValue });

        // Act
        await Handler<NumberControlHandler>().ValidateAnswerAsync(context);

        // Assert
        Assert.NotEmpty(context.Errors);
    }

    [Fact]
    public async Task ConstantSum_ValidateAnswer_ASumTooLargeForADecimal_IsInvalid()
    {
        // Arrange
        var question = Question(SurveyControlNames.ConstantSum);
        question.Rows = [new SurveySubQuestion { Id = "r1", Text = "One" }, new SurveySubQuestion { Id = "r2", Text = "Two" }];
        question.SetSettings(new ConstantSumSettings { Total = 100 });

        var huge = decimal.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var context = Validate(question, new SurveyAnswer { Rows = new Dictionary<string, List<string>> { ["r1"] = [huge], ["r2"] = [huge] } });

        // Act
        await Handler<ConstantSumControlHandler>().ValidateAnswerAsync(context);

        // Assert
        Assert.NotEmpty(context.Errors);
    }

    [Fact]
    public void IndexRows_LongIdsAndHugeNumbers_FitTheirColumns()
    {
        // Arrange
        var question = new SurveyQuestion { Id = "q1", Name = "q1", ControlType = "Indexed" };
        var definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [question] }] };
        var response = new SurveyResponse
        {
            ItemId = "r1",
            SurveyId = "s1",
            Status = SurveyResponseStatus.Completed,
            CompletedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            Answers = new Dictionary<string, SurveyAnswer> { ["q1"] = new SurveyAnswer { Values = ["x"] } },
        };

        var registry = new Moq.Mock<ISurveyControlRegistry>();
        var handler = new Moq.Mock<ISurveyControlHandler>();
        handler.Setup(item => item.GetIndexRows(question, Moq.It.IsAny<SurveyAnswer>())).Returns(
        [
            new SurveyAnswerIndexRow { RowId = new string('r', 150), ChoiceId = new string('c', 150), NumberValue = decimal.MaxValue },
            new SurveyAnswerIndexRow { RowId = "row", NumberValue = 12.5m },
        ]);
        registry.Setup(item => item.GetHandler("Indexed")).Returns(handler.Object);

        // Act
        var rows = SurveyAnswerIndexRows.Build(response, definition, registry.Object);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(SurveyAnswerIndex.KeyLength, rows[0].RowId.Length);
        Assert.Equal(SurveyAnswerIndex.KeyLength, rows[0].ChoiceId.Length);
        Assert.Null(rows[0].NumberValue);
        Assert.Equal(12.5m, rows[1].NumberValue);
    }

    private static T Handler<T>()
        where T : ISurveyControlHandler
    {
        var services = ControlsTestServices.Build().CreateScope().ServiceProvider;

        return services.GetServices<ISurveyControlHandler>().OfType<T>().Single();
    }
}
