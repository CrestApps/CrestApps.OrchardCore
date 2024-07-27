using OrchardCore.Entities;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Models;

public sealed class UserBadgeContext : Entity
{
    public User DisplayUser { get; set; }

    public string DisplayName { get; set; }
}
