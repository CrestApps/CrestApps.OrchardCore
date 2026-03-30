using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.ViewModels;

public class EditDeploymentViewModel
{
    public string Name { get; set; }

    public string ConnectionName { get; set; }

    public string[] SelectedTypes { get; set; }

    [BindNever]
    public bool IsNew { get; set; }

    [BindNever]
    public bool HasContainedConnection { get; set; }

    [BindNever]
    public IList<SelectListItem> Connections { get; set; }

    [BindNever]
    public IList<SelectListItem> Types { get; set; }
}
