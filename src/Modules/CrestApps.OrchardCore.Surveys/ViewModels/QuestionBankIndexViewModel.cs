using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The question banks list.
/// </summary>
public class QuestionBankIndexViewModel
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the banks of the page.
    /// </summary>
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets whether the user may change banks.
    /// </summary>
    public bool CanManage { get; set; }
}
