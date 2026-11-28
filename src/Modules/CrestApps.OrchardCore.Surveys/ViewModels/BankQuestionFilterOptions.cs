using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The filters and bulk action of the questions of a bank.
/// </summary>
public class BankQuestionFilterOptions
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the question type, or empty for all.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the tag, or empty for all.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the difficulty, or empty for all.
    /// </summary>
    public string Difficulty { get; set; }

    /// <summary>
    /// Gets or sets which questions show: active, retired or all.
    /// </summary>
    public string Show { get; set; }

    /// <summary>
    /// Gets or sets the bulk action: retire, restore, move or delete.
    /// </summary>
    public string BulkAction { get; set; }

    /// <summary>
    /// Gets or sets the bank questions move to.
    /// </summary>
    public string TargetBankId { get; set; }

    /// <summary>
    /// Gets or sets the type options.
    /// </summary>
    public List<SelectListItem> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets the tag options.
    /// </summary>
    public List<SelectListItem> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the difficulty options.
    /// </summary>
    public List<SelectListItem> Difficulties { get; set; } = [];

    /// <summary>
    /// Gets or sets the show options.
    /// </summary>
    public List<SelectListItem> Shows { get; set; } = [];

    /// <summary>
    /// Gets or sets the bulk actions.
    /// </summary>
    public List<SelectListItem> BulkActions { get; set; } = [];
}
