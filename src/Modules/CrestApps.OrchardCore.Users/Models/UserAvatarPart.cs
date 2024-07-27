using OrchardCore.ContentManagement;
using OrchardCore.Media.Fields;

namespace CrestApps.OrchardCore.Users.Models;

public sealed class UserAvatarPart : ContentPart
{
    public MediaField Avatar { get; set; }
}
