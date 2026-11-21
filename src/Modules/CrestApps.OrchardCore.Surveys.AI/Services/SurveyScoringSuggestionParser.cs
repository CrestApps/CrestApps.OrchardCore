namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Reads a model's scoring suggestion, keeping only points within the question's range.
/// </summary>
public static class SurveyScoringSuggestionParser
{
    /// <summary>
    /// The longest comment kept.
    /// </summary>
    public const int MaxCommentLength = 1000;

    /// <summary>
    /// Reads a suggestion.
    /// </summary>
    /// <param name="text">The model's answer.</param>
    /// <param name="maxPoints">The most points the question gives.</param>
    /// <returns>The suggestion, or <see langword="null"/> when the answer holds neither points nor a comment.</returns>
    public static SurveyScoringSuggestion Parse(string text, decimal maxPoints)
    {
        var root = SurveysAIJson.ReadObject(text);

        if (root is null)
        {
            return null;
        }

        var suggestion = new SurveyScoringSuggestion
        {
            Comment = SurveysAIJson.String(root["comment"] ?? root["feedback"], MaxCommentLength),
        };

        if (SurveysAIJson.Number(root["points"]) is { } points && points >= 0 && points <= maxPoints)
        {
            suggestion.Points = Math.Round(points, 2, MidpointRounding.AwayFromZero);
        }

        return suggestion.Points is null && suggestion.Comment is null ? null : suggestion;
    }

}
