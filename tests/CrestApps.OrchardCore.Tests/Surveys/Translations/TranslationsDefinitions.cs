using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Translations;

internal static class TranslationsDefinitions
{
    public static SurveyDefinition Create()
        => new()
        {
            Title = "Customer survey",
            Introduction = "<p>Tell us what you think.</p>",
            Pages =
            [
                new SurveyPage
                {
                    Id = "p1",
                    Name = "page_1",
                    Title = "About you",
                    Questions =
                    [
                        new SurveyQuestion
                        {
                            Id = "q1",
                            Name = "q_happy",
                            ControlType = "SingleChoice",
                            Title = "Are you happy?",
                            Placeholder = "Pick one",
                            Choices =
                            [
                                new SurveyChoice { Id = "c1", Value = "1", Text = "Yes" },
                                new SurveyChoice { Id = "c2", Value = "2", Text = "No" },
                            ],
                        },
                        new SurveyQuestion
                        {
                            Id = "q2",
                            Name = "q_grid",
                            ControlType = "Matrix",
                            Title = "Rate us",
                            Rows = [new SurveySubQuestion { Id = "r1", Name = "speed", Text = "Speed" }],
                        },
                    ],
                },
            ],
            Endings = [new SurveyEnding { Id = "e1", Name = "thanks", Title = "Thanks", Message = "<p>Thank you.</p>", IsDefault = true }],
        };
}
