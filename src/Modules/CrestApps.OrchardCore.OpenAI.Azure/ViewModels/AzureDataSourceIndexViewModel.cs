using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

/// <summary>
/// View model for Azure AI data source index selection.
/// Used for Azure AI Search and Elasticsearch data sources.
/// </summary>
public class AzureDataSourceIndexViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string IndexName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> IndexNames { get; set; }
}
