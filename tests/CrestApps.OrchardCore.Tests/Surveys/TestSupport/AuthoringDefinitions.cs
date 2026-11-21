using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Builds small definitions for authoring tests.
/// </summary>
internal static class AuthoringDefinitions
{
    public static SurveyDefinition TwoPages()
        => new()
        {
            Title = "Test",
            Pages =
            [
                new SurveyPage
                {
                    Id = "p1",
                    Name = "page_1",
                    Questions =
                    [
                        Choice("q1", "color", "red", "blue"),
                        new SurveyQuestion { Id = "q2", Name = "comment", ControlType = AuthoringSurveyHarness.TextControl, Title = "Comment" },
                    ],
                },
                new SurveyPage
                {
                    Id = "p2",
                    Name = "page_2",
                    Questions =
                    [
                        new SurveyQuestion { Id = "q3", Name = "why", ControlType = AuthoringSurveyHarness.TextControl, Title = "Why {color}?" },
                    ],
                },
            ],
            Endings = [new SurveyEnding { Id = "e1", Name = "done", IsDefault = true }],
        };

    public static SurveyQuestion Choice(string id, string name, params string[] choiceIds)
        => new()
        {
            Id = id,
            Name = name,
            ControlType = AuthoringSurveyHarness.ChoiceControl,
            Title = name,
            Choices = choiceIds.Select(choiceId => new SurveyChoice { Id = choiceId, Value = choiceId, Text = choiceId }).ToList(),
        };
}
