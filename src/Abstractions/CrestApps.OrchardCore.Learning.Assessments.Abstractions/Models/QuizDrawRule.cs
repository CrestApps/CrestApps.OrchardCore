namespace CrestApps.OrchardCore.Learning.Assessments.Models;

/// <summary>
/// A rule that draws questions at random from a bank when an attempt starts.
/// </summary>
public sealed class QuizDrawRule
{
    /// <summary>
    /// Gets or sets the question bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the tag a question must have, or <see langword="null"/>.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the difficulty a question must have, or <see langword="null"/>.
    /// </summary>
    public int? Difficulty { get; set; }

    /// <summary>
    /// Gets or sets how many questions to draw.
    /// </summary>
    public int Count { get; set; } = 1;
}
