using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Roles.ViewModels;

public class RolePickerViewModel
{
    public string DisplayName { get; set; }

    public string[] Roles { get; set; }

    [BindNever]
    public RolePickerPartSettings Settings { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> AvailableRoles { get; set; }
}
