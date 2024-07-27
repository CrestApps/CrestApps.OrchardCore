using CrestApps.OrchardCore.Users.Core;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users.Services;

[Feature(UsersConstants.Feature.Avatars)]
public sealed class AvatarUserShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("UserBadgeNameContentMetaIcon")
               .Placement(context => new PlacementInfo() { Location = "-" });

        builder.Describe("UserBadgeNameAdmin")
               .Placement(context => new PlacementInfo() { Location = "-" });

        return ValueTask.CompletedTask;
    }
}
