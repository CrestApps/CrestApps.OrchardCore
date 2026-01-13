using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.OpenAI.Azure.ViewModels;

public class AzureDataSourceElasticsearchViewModel
{
    public string IndexName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> IndexNames { get; set; }
}
