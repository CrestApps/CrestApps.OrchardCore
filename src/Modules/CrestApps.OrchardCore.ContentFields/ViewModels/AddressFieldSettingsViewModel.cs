using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.ContentFields.ViewModels;

public class AddressFieldSettingsViewModel
{
    public string Hint { get; set; }

    public bool Required { get; set; }

    public bool ShowName { get; set; }

    public bool ShowCompany { get; set; }

    public bool ShowPhone { get; set; }

    public string[] Countries { get; set; } = [];

    public string DefaultCountry { get; set; }

    [BindNever]
    public IReadOnlyList<SelectListItem> CountryOptions { get; set; } = [];
}
