using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.SmartFields.ViewModels;

public class SmartTextFieldAutocompleteSettingsViewModel
{
    public string ProfileId { get; set; }

    public string Hint { get; set; }

    public SelectListItem[] Profiles { get; set; }
}
