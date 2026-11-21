namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// The score of one question in one completed response.
/// </summary>
/// <param name="ResponseId">The response.</param>
/// <param name="QuestionId">The question.</param>
/// <param name="Score">The points.</param>
/// <param name="MaxPoints">The most points possible.</param>
public sealed record SurveyQuestionScore(string ResponseId, string QuestionId, decimal Score, decimal MaxPoints);
