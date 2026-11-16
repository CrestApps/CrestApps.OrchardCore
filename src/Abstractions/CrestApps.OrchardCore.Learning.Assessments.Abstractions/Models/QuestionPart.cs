using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// What every question has: its prompt is the content item body; this part holds its points, difficulty, tags and
/// general feedback.
/// </summary>
public sealed class QuestionPart : ContentPart
{
    /// <summary>
    /// Gets or sets the points the question is worth.
    /// </summary>
    public decimal Points { get; set; } = 1;

    /// <summary>
    /// Gets or sets the difficulty, from 1 (easy) to 5 (hard).
    /// </summary>
    public int Difficulty { get; set; } = 3;

    /// <summary>
    /// Gets or sets the tags random draws select by.
    /// </summary>
    public IList<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the prompt shown to the learner.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets or sets the feedback shown in the review, whatever the answer.
    /// </summary>
    public string GeneralFeedback { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the question in the system it was imported from, such as the identifier of a QTI
    /// assessment item. Importing a row with the same identifier into the same question bank updates this question.
    /// </summary>
    public string SourceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the file the question was imported from, such as <c>bank.zip/items/choice.xml</c>.
    /// </summary>
    public string SourceFile { get; set; }
}
