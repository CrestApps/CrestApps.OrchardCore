namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A pool of questions on a page from which a number are drawn at random for each respondent.
/// </summary>
public sealed class QuestionPool
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the variable code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label staff see.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how many questions are drawn; 0 draws them all.
    /// </summary>
    public int DrawCount { get; set; }

    /// <summary>
    /// Gets or sets whether the drawn questions show in a random order.
    /// </summary>
    public bool Shuffle { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the least-presented questions are drawn first.
    /// </summary>
    public bool EvenlyPresent { get; set; }

    /// <summary>
    /// Gets or sets the question bank queries that add candidates; resolved when the survey is published.
    /// </summary>
    public List<BankDraw> BankDraws { get; set; } = [];
}
