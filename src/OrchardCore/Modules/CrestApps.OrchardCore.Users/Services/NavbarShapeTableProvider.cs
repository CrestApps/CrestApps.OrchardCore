using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Utilities;

namespace CrestApps.OrchardCore.Users.Services;

public sealed class NavbarShapeTableProvider : IShapeTableProvider
{
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("UserMenuItems")
               .OnDisplaying(displaying =>
               {
                   // UserDisplayNameText_[DisplayType]__DisplayName (e.g., 'UserDisplayNameText-DisplayName.SummaryAdmin')
                   displaying.Shape.Metadata.Alternates.Add($"UserMenuItems_{displaying.Shape.Metadata.DisplayType.EncodeAlternateElement()}__DisplayName");
               });

        return ValueTask.CompletedTask;
    }
}
