using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Controls;
using CrestApps.OrchardCore.Surveys.Core.Controls.ChoiceBlanks;
using CrestApps.OrchardCore.Surveys.Core.Controls.FillInBlanks;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Surveys.Controls;

/// <summary>
/// What respondents see of a question never holds a value of its key, for every built-in question type.
/// </summary>
public sealed class SurveyRespondentProjectionTests
{
    private static readonly string[] _secrets = ["SECRETANSWER", "987654.321", "SECRETFEEDBACK", "SECRETGUIDE", "SECRETKEY", "SECRETBLANK", "[["];

    [Fact]
    public void Project_EveryBuiltInControl_LeavesTheKeyOut()
    {
        using var root = ControlsTestServices.Build();
        var services = root.CreateScope().ServiceProvider;
        var registry = ControlsTestServices.Registry(services);
        var projector = new SurveyRespondentProjector(registry);
        var controls = registry.GetControls();

        Assert.True(controls.Count >= 22);

        foreach (var descriptor in controls)
        {
            var question = new SurveyQuestion { Id = "q1", Name = "q_one", Title = "Title", ControlType = descriptor.Name };
            registry.GetHandler(descriptor.Name).Initialize(question);
            FillKey(question);

            var view = projector.Project(question, new SurveyDefinition { Pages = [new SurveyPage { Questions = [question] }] }, plan: null, hiddenChoiceIds: null, pipe: null);
            var json = JsonSerializer.Serialize(view);

            foreach (var secret in _secrets)
            {
                Assert.False(json.Contains(secret, StringComparison.Ordinal), $"{descriptor.Name} shows '{secret}' to respondents: {json}");
            }
        }
    }

    private static void FillKey(SurveyQuestion question)
    {
        question.Scoring = new SurveyQuestionScoring
        {
            Points = 3,
            CorrectChoiceIds = question.Choices.Select(choice => choice.Id).ToList(),
            AcceptedAnswers = ["SECRETANSWER"],
            NumberAnswer = 987654.321m,
            Pairs = question.Rows.ToDictionary(row => row.Id, row => question.Choices.FirstOrDefault()?.Id ?? "c"),
            GeneralFeedback = "SECRETFEEDBACK",
            CorrectFeedback = "SECRETFEEDBACK",
            IncorrectFeedback = "SECRETFEEDBACK",
            ChoiceFeedback = question.Choices.ToDictionary(choice => choice.Id, _ => "SECRETFEEDBACK"),
            GraderGuidance = "SECRETGUIDE",
            Key = new JsonObject { ["secret"] = "SECRETKEY" },
        };

        if (question.ControlType == SurveyControlNames.FillInBlanks)
        {
            question.SetSettings(new FillInBlanksSettings { Text = "The [[SECRETBLANK]] is here." });
        }

        if (question.ControlType == SurveyControlNames.ChoiceBlanks)
        {
            question.SetSettings(new ChoiceBlanksSettings { Text = "Pick [[alpha]] now.", Distractors = ["beta"] });
        }
    }
}
