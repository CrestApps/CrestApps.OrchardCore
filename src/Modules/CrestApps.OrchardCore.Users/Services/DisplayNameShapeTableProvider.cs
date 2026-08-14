using CrestApps.OrchardCore.Users.Core;
using OrchardCore.DisplayManagement.Descriptors;
using OrchardCore.DisplayManagement.Utilities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Users.Services;

/// <summary>
/// Provides display name shape table functionality.
/// </summary>
[Feature(UsersConstants.Feature.DisplayName)]
public sealed class DisplayNameShapeTableProvider : IShapeTableProvider
{
    /// <summary>
    /// Asynchronously performs the discover operation.
    /// </summary>
    /// <param name="builder">The builder.</param>
    public ValueTask DiscoverAsync(ShapeTableBuilder builder)
    {
        builder.Describe("UserDisplayNameText")
            .OnDisplaying(displaying =>
            {
                // UserDisplayNameText__DisplayName (e.g., 'UserDisplayNameText-DisplayText')
                displaying.Shape.Metadata.Alternates.Add("UserDisplayNameText__DisplayText");

                // UserDisplayNameText_[DisplayType]__DisplayText (e.g., 'UserDisplayNameText-DisplayText.SummaryAdmin')
                displaying.Shape.Metadata.Alternates.Add($"UserDisplayNameText_{displaying.Shape.Metadata.DisplayType.EncodeAlternateElement()}__DisplayText");
            });

        return ValueTask.CompletedTask;
    }
}
