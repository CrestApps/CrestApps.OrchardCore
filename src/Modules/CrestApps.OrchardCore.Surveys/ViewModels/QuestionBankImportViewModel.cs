using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The import page of a question bank.
/// </summary>
public class QuestionBankImportViewModel
{
    /// <summary>
    /// Gets or sets the id of the bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the title of the bank.
    /// </summary>
    public string BankTitle { get; set; }

    /// <summary>
    /// Gets or sets the chosen format.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the uploaded file.
    /// </summary>
    public IFormFile File { get; set; }

    /// <summary>
    /// Gets or sets the formats.
    /// </summary>
    public List<SelectListItem> Formats { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the last post only checked the file.
    /// </summary>
    public bool Checked { get; set; }

    /// <summary>
    /// Gets or sets how many questions the file holds that can be imported.
    /// </summary>
    public int ReadCount { get; set; }

    /// <summary>
    /// Gets or sets how many questions were added.
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Gets or sets how many questions were updated.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Gets or sets the problems found.
    /// </summary>
    public List<string> Errors { get; set; } = [];
}
