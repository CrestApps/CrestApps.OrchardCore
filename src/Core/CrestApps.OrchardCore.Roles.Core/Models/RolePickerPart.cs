using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Roles.Core.Models;

public sealed class RolePickerPart : ContentPart
{
    public string[] RoleNames { get; set; }
}
