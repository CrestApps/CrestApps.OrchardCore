namespace CrestApps.OrchardCore.Roles.ViewModels;

public class RolePickerPartSettingsViewModel
{
    public bool Required { get; set; }

    public bool AllowSelectMultiple { get; set; }

    public string[] ExcludedRoles { get; set; }

    public string Hint { get; set; }
}
