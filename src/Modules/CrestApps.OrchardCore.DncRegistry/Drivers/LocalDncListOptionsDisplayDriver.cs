using CrestApps.OrchardCore.DncRegistry.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.DncRegistry.Drivers;

/// <summary>
/// Display driver that provides header/toolbar shapes for the local DNC list admin page.
/// </summary>
public sealed class LocalDncListOptionsDisplayDriver : DisplayDriver<LocalDncListOptions>
{
    /// <summary>
    /// Builds the editor shapes for the list options header.
    /// </summary>
    /// <param name="model">The list options.</param>
    /// <param name="context">The build editor context.</param>
    public override Task<IDisplayResult> EditAsync(LocalDncListOptions model, BuildEditorContext context)
    {
        return CombineAsync(
            Initialize<LocalDncListOptions>("LocalDncListAdminListSummary", m =>
            {
                m.StartIndex = model.StartIndex;
                m.EndIndex = model.EndIndex;
                m.TotalItemCount = model.TotalItemCount;
            }).Location("Summary:10"),
            Initialize<LocalDncListOptions>("LocalDncListAdminListCreate", m =>
            {
                m.TotalItemCount = model.TotalItemCount;
            }).Location("Create:10")
        );
    }
}
