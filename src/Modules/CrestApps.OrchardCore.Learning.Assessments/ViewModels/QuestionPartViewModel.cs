namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The editor of the question part.
/// </summary>
public class QuestionPartViewModel
{
    /// <summary>Gets or sets the prompt.</summary>
    public string Prompt { get; set; }

    /// <summary>Gets or sets the points.</summary>
    public decimal Points { get; set; } = 1;

    /// <summary>Gets or sets the difficulty.</summary>
    public int Difficulty { get; set; } = 3;

    /// <summary>Gets or sets the tags, comma separated.</summary>
    public string Tags { get; set; }

    /// <summary>Gets or sets the general feedback.</summary>
    public string GeneralFeedback { get; set; }
}
