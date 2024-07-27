using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Users.Models;

public sealed class UserFullNamePart : ContentPart
{
    public string DisplayName { get; set; }

    public string FirstName { get; set; }

    public string LastName { get; set; }

    public string MiddleName { get; set; }
}
