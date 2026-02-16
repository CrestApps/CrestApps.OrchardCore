using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditAIDataSourceRagViewModel
{
    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public bool IsInScope { get; set; } = true;

    public string Filter { get; set; }

    /// <summary>
    /// The source index provider name, used for showing provider-specific filter hints.
    /// </summary>
    [BindNever]
    public string SourceProviderName { get; set; }
}
