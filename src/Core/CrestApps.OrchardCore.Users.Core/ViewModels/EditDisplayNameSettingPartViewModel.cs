using CrestApps.OrchardCore.Users.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Users.Core.ViewModels;

public class EditDisplayNameSettingPartViewModel
{
    public DisplayNameType Type { get; set; }

    public DisplayNamePropertyType DisplayName { get; set; }

    public DisplayNamePropertyType FirstName { get; set; }

    public DisplayNamePropertyType LastName { get; set; }

    public DisplayNamePropertyType MiddleName { get; set; }

    public string Template { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> Types { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> PropertyTypes { get; set; }
}
