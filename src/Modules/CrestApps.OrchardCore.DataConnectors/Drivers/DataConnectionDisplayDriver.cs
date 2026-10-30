using System.Text.RegularExpressions;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DataConnectors.Drivers;

/// <summary>
/// Renders the row of a connection in the connections list, and the fields every connection has: its names and
/// description.
/// </summary>
internal sealed partial class DataConnectionDisplayDriver : DisplayDriver<DataConnection>
{
    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataConnectorManager _connectorManager;

    internal readonly IStringLocalizer S;

    public DataConnectionDisplayDriver(
        IDataConnectionStore connectionStore,
        IDataConnectorManager connectorManager,
        IStringLocalizer<DataConnectionDisplayDriver> stringLocalizer)
    {
        _connectionStore = connectionStore;
        _connectorManager = connectorManager;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(DataConnection connection, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = connection.DisplayText?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("DataConnection_Fields_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("DataConnection_Description_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("DataConnection_Tags_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Connector:5"),
            View("DataConnection_Meta_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("DataConnection_Buttons_SummaryAdmin", connection).Location("Actions:5"),
            View("DataConnection_ActionsMenu_SummaryAdmin", connection).Location("ActionsMenu:10")
        );
    }

    public override IDisplayResult Edit(DataConnection connection, BuildEditorContext context)
    {
        return Initialize<DataConnectionFieldsViewModel>("DataConnectionFields_Edit", model =>
        {
            var descriptor = _connectorManager.GetDescriptor(connection.Connector);

            model.DisplayText = connection.DisplayText;
            model.Name = connection.Name;
            model.Description = connection.Description;
            model.ConnectorDisplayName = descriptor?.DisplayName.Value ?? connection.Connector;
            model.ConnectorDescription = descriptor?.Description.Value;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(DataConnection connection, UpdateEditorContext context)
    {
        var model = new DataConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The name is required."]);
        }

        var name = string.IsNullOrWhiteSpace(model.Name)
            ? (string.IsNullOrWhiteSpace(model.DisplayText) ? null : ConnectedDataSetFields.ToFieldName(model.DisplayText))
            : model.Name.Trim();

        if (string.IsNullOrEmpty(name) || !TechnicalName().IsMatch(name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The technical name must start with a letter and hold only letters, digits and underscores."]);
        }
        else if (await _connectionStore.FindByNameAsync(name) is { } existing && existing.ItemId != connection.ItemId)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another connection already has the technical name '{0}'.", name]);
        }

        connection.DisplayText = model.DisplayText?.Trim();
        connection.Name = name;
        connection.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

        return Edit(connection, context);
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,127}$")]
    internal static partial Regex TechnicalName();
}
