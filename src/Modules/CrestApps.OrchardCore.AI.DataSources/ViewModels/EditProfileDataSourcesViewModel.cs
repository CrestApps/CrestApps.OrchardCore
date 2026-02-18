using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.DataSources.ViewModels;

public class EditProfileDataSourcesViewModel
{
    public string DataSourceId { get; set; }

    public bool EnableEarlyRag { get; set; }

    public int? Strictness { get; set; }

    public int? TopNDocuments { get; set; }

    public bool IsInScope { get; set; }

    public string Filter { get; set; }

    [BindNever]
    public IEnumerable<AIDataSource> DataSources { get; set; }
}
