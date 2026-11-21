namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The form of a question bank.
/// </summary>
public class QuestionBankEditViewModel
{
    /// <summary>
    /// Gets or sets the id, or <see langword="null"/> for a new bank.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the tags, separated by commas.
    /// </summary>
    public string Tags { get; set; }

    /// <summary>
    /// Gets whether the bank is new.
    /// </summary>
    public bool IsNew => string.IsNullOrEmpty(Id);
}
