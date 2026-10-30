using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.DataSources;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DataConnectors.Drivers;

/// <summary>
/// Renders the row of a connected data set, and the fields every data set has: its names, resource, access mode,
/// fields and refresh schedule.
/// </summary>
internal sealed class ConnectedDataSetDisplayDriver : DisplayDriver<ConnectedDataSet>
{
    private static readonly TimeSpan _discoveryTimeout = TimeSpan.FromSeconds(20);

    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly DataSetConnectionResolver _resolver;
    private readonly ConnectedDataReader _reader;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public ConnectedDataSetDisplayDriver(
        IConnectedDataSetStore dataSetStore,
        DataSetConnectionResolver resolver,
        ConnectedDataReader reader,
        IClock clock,
        ILogger<ConnectedDataSetDisplayDriver> logger,
        IStringLocalizer<ConnectedDataSetDisplayDriver> stringLocalizer)
    {
        _dataSetStore = dataSetStore;
        _resolver = resolver;
        _reader = reader;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ConnectedDataSet dataSet, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = dataSet.DisplayText?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("ConnectedDataSet_Fields_SummaryAdmin", dataSet)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ConnectedDataSet_Status_SummaryAdmin", dataSet)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Refresh:1"),
            Initialize<ConnectedDataSetRowViewModel>("ConnectedDataSet_Tags_SummaryAdmin", async model =>
            {
                model.DataSet = dataSet;
                model.Connection = await _resolver.GetConnectionAsync(dataSet);
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Source:5"),
            View("ConnectedDataSet_Meta_SummaryAdmin", dataSet)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"),
            View("ConnectedDataSet_Buttons_SummaryAdmin", dataSet).Location("Actions:5"),
            View("ConnectedDataSet_ActionsMenu_SummaryAdmin", dataSet).Location("ActionsMenu:10")
        );
    }

    public override IDisplayResult Edit(ConnectedDataSet dataSet, BuildEditorContext context)
    {
        return Initialize<ConnectedDataSetFieldsViewModel>("ConnectedDataSetFields_Edit", async model =>
        {
            var connection = await _resolver.GetConnectionAsync(dataSet);
            var descriptor = await _resolver.GetConnectorAsync(dataSet);

            model.DisplayText = dataSet.DisplayText;
            model.Name = dataSet.Name;
            model.Description = dataSet.Description;
            model.Resource = dataSet.Resource;
            model.AccessMode = dataSet.AccessMode;
            model.IsEnabled = dataSet.IsEnabled;
            model.DefaultDateField = dataSet.DefaultDateField;
            model.RefreshIntervalMinutes = dataSet.Refresh?.IntervalMinutes;
            model.RefreshMode = dataSet.Refresh?.Mode ?? DataSetRefreshMode.Full;
            model.IncrementalField = dataSet.Refresh?.IncrementalField;
            model.SchemaChangePolicy = dataSet.Refresh?.SchemaChangePolicy ?? DataSetSchemaChangePolicy.Accept;
            model.IsNew = context.IsNew;
            model.ConnectionDisplayText = connection?.DisplayText ?? S["(missing connection)"];
            model.ConnectorDisplayName = descriptor?.DisplayName.Value ?? connection?.Connector;
            model.SupportsLive = descriptor?.Supports(DataConnectorCapabilities.Live) == true;
            model.SupportsImport = descriptor?.Supports(DataConnectorCapabilities.Import) == true;
            model.SupportsIncremental = descriptor?.Supports(DataConnectorCapabilities.IncrementalImport) == true;
            model.Import = dataSet.Import;
            model.PermissionName = string.IsNullOrEmpty(dataSet.Name) ? null : DataConnectorPermissions.CreateReadPermission(dataSet.Name).Name;
            model.FieldTypes = Enum.GetValues<DataFieldType>().Select(type => new SelectListItem(type.ToString(), type.ToString())).ToArray();
            model.Fields =
            [
                .. (dataSet.Fields ?? []).Select(field => new ConnectedDataFieldViewModel
                {
                    Name = field.Name,
                    SourceName = field.SourceName,
                    DisplayName = field.DisplayName,
                    Type = field.Type,
                    SourceType = field.SourceType,
                    IsIdentifier = field.IsIdentifier,
                    IsIncluded = field.IsIncluded,
                }),
            ];

            if (connection is not null && descriptor?.Supports(DataConnectorCapabilities.Discovery) == true)
            {
                (model.Resources, model.DiscoveryError) = await DiscoverAsync(connection);
            }
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ConnectedDataSet dataSet, UpdateEditorContext context)
    {
        var model = new ConnectedDataSetFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var descriptor = await _resolver.GetConnectorAsync(dataSet);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The name is required."]);
        }

        // Consumers and the read permission are keyed by the technical name, so it is chosen once.
        if (context.IsNew)
        {
            var name = string.IsNullOrWhiteSpace(model.Name)
                ? (string.IsNullOrWhiteSpace(model.DisplayText) ? null : ConnectedDataSetFields.ToFieldName(model.DisplayText))
                : model.Name.Trim();

            if (string.IsNullOrEmpty(name) || !DataConnectionDisplayDriver.TechnicalName().IsMatch(name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["The technical name must start with a letter and hold only letters, digits and underscores."]);
            }
            else if (await _dataSetStore.FindByNameAsync(name) is { } existing && existing.ItemId != dataSet.ItemId)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another data set already has the technical name '{0}'.", name]);
            }

            dataSet.Name = name;
        }

        if (model.AccessMode == DataSetAccessMode.Live && descriptor?.Supports(DataConnectorCapabilities.Live) != true)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AccessMode), S["This connector can't read data sets live; import it instead."]);
        }

        if (model.AccessMode == DataSetAccessMode.Imported && descriptor?.Supports(DataConnectorCapabilities.Import) != true)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AccessMode), S["This connector can't import data sets; read it live instead."]);
        }

        var fields = new List<ConnectedDataField>();

        foreach (var field in model.Fields ?? [])
        {
            if (string.IsNullOrWhiteSpace(field?.SourceName))
            {
                continue;
            }

            var fieldName = string.IsNullOrWhiteSpace(field.Name) ? ConnectedDataSetFields.ToFieldName(field.SourceName) : field.Name.Trim();

            if (!DataConnectionDisplayDriver.TechnicalName().IsMatch(fieldName))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Fields), S["The field name '{0}' must start with a letter and hold only letters, digits and underscores.", fieldName]);
            }
            else if (fields.Any(existing => string.Equals(existing.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Fields), S["Two fields are named '{0}'.", fieldName]);
            }

            fields.Add(new ConnectedDataField
            {
                Name = fieldName,
                SourceName = field.SourceName,
                DisplayName = string.IsNullOrWhiteSpace(field.DisplayName) ? fieldName : field.DisplayName.Trim(),
                Type = field.Type,
                SourceType = field.SourceType,
                IsIdentifier = field.IsIdentifier,
                IsIncluded = field.IsIncluded,
            });
        }

        var dateField = fields.FirstOrDefault(field => field.IsIncluded && field.Name == model.DefaultDateField && DataValues.IsTemporal(field.Type));
        var refresh = new DataSetRefreshSettings
        {
            Mode = model.RefreshMode,
            IncrementalField = string.IsNullOrWhiteSpace(model.IncrementalField) ? null : model.IncrementalField,
            IntervalMinutes = model.RefreshIntervalMinutes,
            SchemaChangePolicy = model.SchemaChangePolicy,
        };

        if (model.AccessMode == DataSetAccessMode.Imported)
        {
            if (refresh.IntervalMinutes is < 1 or > 525_600)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.RefreshIntervalMinutes), S["The refresh interval must be between 1 minute and one year, or empty to refresh only on demand."]);
            }

            if (refresh.Mode == DataSetRefreshMode.Incremental && fields.Count > 0)
            {
                if (descriptor?.Supports(DataConnectorCapabilities.IncrementalImport) != true)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.RefreshMode), S["This connector can't refresh incrementally."]);
                }
                else if (!fields.Any(field => field.IsIdentifier))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.RefreshMode), S["An incremental refresh needs at least one identifier field, to replace the rows that changed."]);
                }
                else if (!fields.Any(field => field.Name == refresh.IncrementalField && (DataValues.IsTemporal(field.Type) || DataValues.IsNumeric(field.Type))))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.IncrementalField), S["An incremental refresh needs a date or number field that grows when a row changes."]);
                }
            }
        }

        dataSet.DisplayText = model.DisplayText?.Trim();
        dataSet.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        dataSet.Resource = string.IsNullOrWhiteSpace(model.Resource) ? null : model.Resource.Trim();
        dataSet.AccessMode = model.AccessMode;
        dataSet.IsEnabled = model.IsEnabled;
        dataSet.Fields = fields;
        dataSet.DefaultDateField = dateField?.Name;
        dataSet.Refresh = refresh;
        dataSet.Import ??= new DataSetImportState();
        dataSet.Import.NextRefreshUtc = refresh.IntervalMinutes is > 0 and var minutes
            ? dataSet.Import.LastRefreshUtc?.AddMinutes(minutes) ?? _clock.UtcNow
            : null;

        return Edit(dataSet, context);
    }

    private async Task<(IReadOnlyList<DataConnectorResource> Resources, string Error)> DiscoverAsync(DataConnection connection)
    {
        using var timeout = new CancellationTokenSource(_discoveryTimeout);

        try
        {
            return (await _reader.DiscoverAsync(connection, timeout.Token), null);
        }
        catch (DataConnectorException exception)
        {
            return ([], exception.Message);
        }
        catch (OperationCanceledException)
        {
            return ([], S["The connection didn't list its resources within {0} seconds.", _discoveryTimeout.TotalSeconds].Value);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "The resources of the data connection '{Connection}' couldn't be listed.", connection.Name);

            return ([], S["The resources couldn't be listed. See the site's log for details."].Value);
        }
    }
}
