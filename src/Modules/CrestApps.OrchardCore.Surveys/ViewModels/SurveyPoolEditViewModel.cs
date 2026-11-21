namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// One question pool in the page editor, with at most one question bank draw.
/// </summary>
public class SurveyPoolEditViewModel
{
    /// <summary>
    /// Gets or sets the id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the code.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how many questions are drawn.
    /// </summary>
    public int DrawCount { get; set; }

    /// <summary>
    /// Gets or sets whether the drawn questions are shuffled.
    /// </summary>
    public bool Shuffle { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the least-presented questions are drawn first.
    /// </summary>
    public bool EvenlyPresent { get; set; }

    /// <summary>
    /// Gets or sets the question bank drawn from, or empty.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the tags bank questions must have, separated by commas.
    /// </summary>
    public string BankTags { get; set; }

    /// <summary>
    /// Gets or sets the difficulty bank questions must have, or <see langword="null"/>.
    /// </summary>
    public int? BankDifficulty { get; set; }

    /// <summary>
    /// Gets or sets the most bank questions added, or <see langword="null"/>.
    /// </summary>
    public int? BankMaxCandidates { get; set; }
}
