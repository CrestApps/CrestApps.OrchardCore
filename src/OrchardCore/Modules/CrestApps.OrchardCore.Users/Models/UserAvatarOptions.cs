using System.ComponentModel;

namespace CrestApps.OrchardCore.Users.Models;

public class UserAvatarOptions
{
    public bool Required { get; set; }

    [DefaultValue(true)]
    public bool UseDefaultStyle { get; set; } = true;
}
