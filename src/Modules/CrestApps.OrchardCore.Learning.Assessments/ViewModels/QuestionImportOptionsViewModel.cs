using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Learning.Assessments.ViewModels;

/// <summary>
/// The question bank of a question import.
/// </summary>
public class QuestionImportOptionsViewModel
{
    /// <summary>Gets or sets the question bank, or empty for none.</summary>
    public string QuestionBankId { get; set; }

    /// <summary>Gets or sets the banks the person may edit.</summary>
    [BindNever]
    public IList<SelectListItem> Banks { get; set; } = [];
}
