using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

public class AzureProfileSearchAIViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string IndexName { get; set; }

    [Range(1, 5)]
    public int? Strictness { get; set; }

    [Range(3, 20)]
    public int? TopNDocuments { get; set; }

    public string Filter { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> IndexNames { get; set; }
}
