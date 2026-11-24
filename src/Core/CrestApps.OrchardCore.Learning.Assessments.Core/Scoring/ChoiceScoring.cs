using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// The snapshot shared by single and multiple choice questions.
/// </summary>
internal static class ChoiceScoring
{
    public static void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<ChoiceQuestionPart>(nameof(ChoiceQuestionPart)) ?? new ChoiceQuestionPart();
        var options = part.Options.Where(option => !string.IsNullOrEmpty(option?.Id)).ToList();

        if (part.ShuffleOptions)
        {
            options = ScoringText.Shuffle(options, random);
        }

        item.Choices = options.Select(option => new AttemptChoice { Id = option.Id, Text = option.Text, Feedback = option.Feedback }).ToList();
        item.Key.ChoiceIds = part.Options.Where(option => option?.Correct == true).Select(option => option.Id).ToList();
        item.Key.PartialCredit = part.PartialCredit;
    }
}
