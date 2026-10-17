using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.DisplayManagement.Zones;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Builds the rows of the admin lists of activities. The admin list builds a row with the display type of its layout
/// only, so the display group of the row (scheduled or completed work), its checkbox and its contact are carried by the
/// <see cref="OmnichannelActivityListEntry"/> it is built from.
/// </summary>
internal sealed class OmnichannelActivityListEntryDisplayManager : IAdminListItemDisplayManager<OmnichannelActivityListEntry>
{
    private readonly IDisplayManager<OmnichannelActivityContainer> _displayManager;
    private readonly IShapeFactory _shapeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelActivityListEntryDisplayManager"/> class.
    /// </summary>
    /// <param name="displayManager">The display manager of the activity containers.</param>
    /// <param name="shapeFactory">The shape factory.</param>
    public OmnichannelActivityListEntryDisplayManager(
        IDisplayManager<OmnichannelActivityContainer> displayManager,
        IShapeFactory shapeFactory)
    {
        _displayManager = displayManager;
        _shapeFactory = shapeFactory;
    }

    /// <inheritdoc />
    public async Task<IShape> BuildDisplayAsync(OmnichannelActivityListEntry model, IUpdateModel updater, string displayType)
    {
        var row = await _displayManager.BuildDisplayAsync(model.Container, updater, displayType, model.GroupId ?? string.Empty);

        if (row is not IZoneHolding zones)
        {
            return row;
        }

        var isGrid = displayType == OrchardCoreConstants.DisplayType.SummaryAdminGrid;

        if (model.Selectable)
        {
            await zones.Zones[isGrid ? "Select" : "Checkbox"].AddAsync(
                await CreateShapeAsync("OmnichannelActivityContainer_Checkbox_SummaryAdmin", model.Container), "1");
        }

        if (model.ShowContact && model.Container.Contact is not null)
        {
            await zones.Zones[isGrid ? "Contact" : "Tags"].AddAsync(
                await CreateShapeAsync("OmnichannelActivityContainer_Contact_SummaryAdmin", model.Container), "1");
        }

        return row;
    }

    private ValueTask<IShape> CreateShapeAsync(string shapeType, OmnichannelActivityContainer container)
        => _shapeFactory.CreateAsync(shapeType, () => ValueTask.FromResult<IShape>(new ShapeViewModel<OmnichannelActivityContainer>(shapeType, container)));
}
