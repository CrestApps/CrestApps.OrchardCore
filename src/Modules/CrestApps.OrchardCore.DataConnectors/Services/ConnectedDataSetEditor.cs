using CrestApps.OrchardCore.DataConnectors.Drivers;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// The checks and the mapping of the fields every connected data set has (names, resource, access mode, fields and
/// refresh schedule), shared by the data set editor and the AI tools. Each problem is reported with the name of the
/// editor field it belongs to. The connector's own settings of a data set are edited apart.
/// </summary>
internal sealed class ConnectedDataSetEditor
{
    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedDataSetEditor"/> class.
    /// </summary>
    /// <param name="dataSetStore">The data sets.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConnectedDataSetEditor(
        IConnectedDataSetStore dataSetStore,
        IClock clock,
        IStringLocalizer<ConnectedDataSetEditor> stringLocalizer)
    {
        _dataSetStore = dataSetStore;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Checks the entered fields of a data set and applies them to it.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="model">The entered fields.</param>
    /// <param name="descriptor">The connector of the data set's connection, or <see langword="null"/> when it isn't enabled.</param>
    /// <param name="isNew">Whether the data set is being created; only then is its technical name set.</param>
    /// <param name="addError">Receives each problem with the name of its field.</param>
    /// <returns>A task.</returns>
    public async Task ApplyAsync(
        ConnectedDataSet dataSet,
        ConnectedDataSetFieldsViewModel model,
        DataConnectorDescriptor descriptor,
        bool isNew,
        Action<string, string> addError)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(addError);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            addError(nameof(model.DisplayText), S["The name is required."]);
        }

        // Consumers and the read permission are keyed by the technical name, so it is chosen once.
        if (isNew)
        {
            var name = string.IsNullOrWhiteSpace(model.Name)
                ? (string.IsNullOrWhiteSpace(model.DisplayText) ? null : ConnectedDataSetFields.ToFieldName(model.DisplayText))
                : model.Name.Trim();

            if (string.IsNullOrEmpty(name) || !DataConnectionDisplayDriver.TechnicalName().IsMatch(name))
            {
                addError(nameof(model.Name), S["The technical name must start with a letter and hold only letters, digits and underscores."]);
            }
            else if (await _dataSetStore.FindByNameAsync(name) is { } existing && existing.ItemId != dataSet.ItemId)
            {
                addError(nameof(model.Name), S["Another data set already has the technical name '{0}'.", name]);
            }

            dataSet.Name = name;
        }

        if (model.AccessMode == DataSetAccessMode.Live && descriptor?.Supports(DataConnectorCapabilities.Live) != true)
        {
            addError(nameof(model.AccessMode), S["This connector can't read data sets live; import it instead."]);
        }

        if (model.AccessMode == DataSetAccessMode.Imported && descriptor?.Supports(DataConnectorCapabilities.Import) != true)
        {
            addError(nameof(model.AccessMode), S["This connector can't import data sets; read it live instead."]);
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
                addError(nameof(model.Fields), S["The field name '{0}' must start with a letter and hold only letters, digits and underscores.", fieldName]);
            }
            else if (fields.Any(existing => string.Equals(existing.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                addError(nameof(model.Fields), S["Two fields are named '{0}'.", fieldName]);
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
                addError(nameof(model.RefreshIntervalMinutes), S["The refresh interval must be between 1 minute and one year, or empty to refresh only on demand."]);
            }

            if (refresh.Mode == DataSetRefreshMode.Incremental && fields.Count > 0)
            {
                if (descriptor?.Supports(DataConnectorCapabilities.IncrementalImport) != true)
                {
                    addError(nameof(model.RefreshMode), S["This connector can't refresh incrementally."]);
                }
                else if (!fields.Any(field => field.IsIdentifier))
                {
                    addError(nameof(model.RefreshMode), S["An incremental refresh needs at least one identifier field, to replace the rows that changed."]);
                }
                else if (!fields.Any(field => field.Name == refresh.IncrementalField && (DataValues.IsTemporal(field.Type) || DataValues.IsNumeric(field.Type))))
                {
                    addError(nameof(model.IncrementalField), S["An incremental refresh needs a date or number field that grows when a row changes."]);
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
    }
}
