using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Core.Models;

public class ModelOptions<TOptions>
{
    public string Search { get; set; }

    public TOptions BulkAction { get; set; }

    [BindNever]
    public List<SelectListItem> BulkActions { get; set; }
}

public class ModelOptions : ModelOptions<ModelAction>;
