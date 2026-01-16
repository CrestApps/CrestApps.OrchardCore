using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditProfileDataSourcesViewModel
{
    public string DataSourceId { get; set; }

    [BindNever]
    public IEnumerable<AIDataSource> DataSources { get; set; }
}
