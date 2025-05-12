using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditProfileDataSourcesViewModel
{
    public string DataSourceId { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DataSources { get; set; }
}
