namespace CrestApps.OrchardCore.Roles.Core.Models;

public sealed class RolePickerPartSettings
{
    public bool Required { get; set; }

    public bool AllowSelectMultiple { get; set; }

    public string[] ExcludedRoles { get; set; }

    public string Hint { get; set; }
}
