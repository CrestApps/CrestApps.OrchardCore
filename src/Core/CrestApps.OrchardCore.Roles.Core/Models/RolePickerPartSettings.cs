using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Roles.Core.Models;

public sealed class RolePickerPartSettings : ContentPart
{
    public bool Required { get; set; }

    public bool AllowSelectMultiple { get; set; }

    public string[] ExcludedRoles { get; set; }

    public string Hint { get; set; }
}
