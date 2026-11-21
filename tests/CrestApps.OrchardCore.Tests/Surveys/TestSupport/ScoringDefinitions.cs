using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Builds definitions and responses for scoring tests.
/// </summary>
internal static class ScoringDefinitions
{
    public static SurveyDefinition Quiz(params (string Id, decimal Points)[] questions)
        => Quiz(questions.Select(question => (question.Id, question.Points, (string)null)).ToArray());

    public static SurveyDefinition Quiz(params (string Id, decimal Points, string Category)[] questions)
        => new()
        {
            Scoring = new SurveyScoringSettings { Enabled = true, RoundingDecimals = 2 },
            Pages =
            [
                new SurveyPage
                {
                    Id = "p1",
                    Name = "page1",
                    Questions = questions.Select(question => new SurveyQuestion
                    {
                        Id = question.Id,
                        Name = question.Id,
                        ControlType = ScoringFakeScorer.Type,
                        Scoring = new SurveyQuestionScoring { Points = question.Points, CategoryId = question.Category },
                    }).ToList(),
                },
            ],
        };

    public static SurveyResponse Response(params (string Id, decimal Points)[] answers)
    {
        var response = new SurveyResponse
        {
            ItemId = "response1",
            SurveyId = "survey1",
            SurveyVersionId = "version1",
            VersionNumber = 1,
            Status = SurveyResponseStatus.Completed,
        };

        foreach (var (id, points) in answers)
        {
            response.Answers[id] = new SurveyAnswer { Number = points, Values = ["x"] };
        }

        return response;
    }
}
