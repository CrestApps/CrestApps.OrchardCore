using CrestApps.OrchardCore.Users.Core;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Utilities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users.Services;

[Feature(UsersConstants.Feature.Avatars)]
public sealed class AvatarShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("UserDisplayNameIcon")
               .OnDisplaying(displaying =>
               {
                   // UserDisplayNameIcon__DisplayIcon (e.g., 'UserDisplayNameIcon-DisplayIcon')
                   displaying.Shape.Metadata.Alternates.Add("UserDisplayNameIcon__DisplayIcon");

                   // UserDisplayNameIcon_[DisplayType]__DisplayIcon (e.g., 'UserDisplayNameIcon-DisplayIcon.SummaryAdmin')
                   displaying.Shape.Metadata.Alternates.Add($"UserDisplayNameIcon_{displaying.Shape.Metadata.DisplayType.EncodeAlternateElement()}__DisplayIcon");
               });

        return ValueTask.CompletedTask;
    }
}
