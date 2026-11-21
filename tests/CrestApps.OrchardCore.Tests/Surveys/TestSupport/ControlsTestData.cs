using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Builds questions, forms and contexts for control tests.
/// </summary>
public static class ControlsTestData
{
    public static SurveyQuestion Question(string controlType, params (string Id, string Text)[] choices)
        => new()
        {
            Id = "q1",
            Name = "q_one",
            Title = "Question one",
            ControlType = controlType,
            Choices = choices.Select((choice, index) => new SurveyChoice { Id = choice.Id, Text = choice.Text, Value = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) }).ToList(),
        };

    public static IFormCollection Form(params (string Key, string[] Values)[] values)
        => new FormCollection(values.ToDictionary(value => value.Key, value => new StringValues(value.Values)));

    public static SurveyAnswerReadContext Read(SurveyQuestion question, IFormCollection form, SurveyResponse response = null, SurveyAnswer existing = null)
        => new()
        {
            Question = question,
            Form = form,
            Prefix = $"q.{question.Id}",
            Definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [question] }] },
            Response = response ?? new SurveyResponse { ItemId = "r1" },
            Existing = existing,
        };

    public static SurveyAnswerReadContext ReadJson(SurveyQuestion question, JsonNode json)
        => new()
        {
            Question = question,
            Json = json,
            Prefix = $"q.{question.Id}",
            Definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [question] }] },
            Response = new SurveyResponse { ItemId = "r1" },
        };

    public static SurveyAnswerValidationContext Validate(SurveyQuestion question, SurveyAnswer answer, bool required = false)
        => new()
        {
            Question = question,
            Answer = answer,
            IsRequired = required,
            Definition = new SurveyDefinition { Pages = [new SurveyPage { Id = "p1", Questions = [question] }] },
            Response = new SurveyResponse { ItemId = "r1" },
        };

    public static SurveyScoringContext ScoringContext(bool negativeMarking = false)
        => new() { Settings = new SurveyScoringSettings { Enabled = true, NegativeMarking = negativeMarking } };
}
