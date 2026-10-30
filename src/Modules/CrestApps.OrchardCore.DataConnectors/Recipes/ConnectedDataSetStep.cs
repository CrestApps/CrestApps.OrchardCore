using CrestApps.OrchardCore.DataConnectors.Drivers;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.DataConnectors.Recipes;

/// <summary>
/// Creates or updates connected data sets. A data set is matched by its identifier, then by its technical name, and
/// reads the connection with its connection's identifier or, failing that, its connection's technical name.
/// </summary>
/// <remarks>
/// The rows of an imported data set are never part of a recipe: a data set that has none yet is queued for its first
/// import, which the refresh task runs within a minute.
/// </remarks>
internal sealed class ConnectedDataSetStep : NamedRecipeStepHandler
{
    public const string StepKey = "ConnectedDataSet";

    public const string EntriesMemberName = "DataSets";

    private static readonly TimeSpan _discoveryTimeout = TimeSpan.FromSeconds(30);

    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataConnectorManager _connectorManager;
    private readonly ConnectedDataReader _reader;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedDataSetStep"/> class.
    /// </summary>
    /// <param name="dataSetStore">The data set store.</param>
    /// <param name="connectionStore">The connection store.</param>
    /// <param name="connectorManager">The registered connectors.</param>
    /// <param name="reader">The reader that describes a data set's fields.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConnectedDataSetStep(
        IConnectedDataSetStore dataSetStore,
        IDataConnectionStore connectionStore,
        IDataConnectorManager connectorManager,
        ConnectedDataReader reader,
        IClock clock,
        ILogger<ConnectedDataSetStep> logger,
        IStringLocalizer<ConnectedDataSetStep> stringLocalizer)
        : base(StepKey)
    {
        _dataSetStore = dataSetStore;
        _connectionStore = connectionStore;
        _connectorManager = connectorManager;
        _reader = reader;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var (_, model) in DataConnectorRecipeJson.ReadEntries<ConnectedDataSetRecipeEntry>(context.Step, EntriesMemberName))
        {
            if (model is null)
            {
                context.Errors.Add(S["Each entry of the '{0}' step must be an object.", StepKey]);

                continue;
            }

            var errors = new List<string>();

            await ImportAsync(model, errors);

            foreach (var error in errors)
            {
                context.Errors.Add(error);
            }
        }
    }

    private async Task ImportAsync(ConnectedDataSetRecipeEntry model, List<string> errors)
    {
        var displayText = string.IsNullOrWhiteSpace(model.DisplayText) ? model.Name?.Trim() : model.DisplayText.Trim();
        var requestedName = string.IsNullOrWhiteSpace(model.Name)
            ? (string.IsNullOrEmpty(displayText) ? null : ConnectedDataSetFields.ToFieldName(displayText))
            : model.Name.Trim();

        var dataSet = string.IsNullOrEmpty(model.ItemId) ? null : await _dataSetStore.FindByIdAsync(model.ItemId);

        if (dataSet is null && !string.IsNullOrEmpty(requestedName))
        {
            dataSet = await _dataSetStore.FindByNameAsync(requestedName);
        }

        var isNew = dataSet is null;

        // Consumers and the read permission are keyed by the technical name, so it is chosen once.
        var name = isNew ? requestedName : dataSet.Name;

        if (isNew && (string.IsNullOrEmpty(name) || !DataConnectionDisplayDriver.TechnicalName().IsMatch(name)))
        {
            errors.Add(S["The connected data set '{0}' needs a technical name that starts with a letter and holds only letters, digits and underscores.", name ?? displayText]);

            return;
        }

        if (!isNew && !string.IsNullOrWhiteSpace(model.Name) && !string.Equals(model.Name.Trim(), dataSet.Name, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(S["The connected data set '{0}' can't be renamed to '{1}': reports and pipelines store its technical name.", dataSet.Name, model.Name.Trim()]);

            return;
        }

        var connection = await FindConnectionAsync(model) ?? (isNew ? null : await _connectionStore.FindByIdAsync(dataSet.ConnectionId));

        if (connection is null)
        {
            errors.Add(S["The connected data set '{0}' reads a connection that doesn't exist: '{1}'.", name, model.ConnectionName ?? model.ConnectionId]);

            return;
        }

        if (_connectorManager.GetDescriptor(connection.Connector) is not { } descriptor)
        {
            errors.Add(S["The connected data set '{0}' reads the connection '{1}', whose connector '{2}' isn't enabled.", name, connection.Name, connection.Connector]);

            return;
        }

        if (!isNew && dataSet.ConnectionId != connection.ItemId
            && await _connectionStore.FindByIdAsync(dataSet.ConnectionId) is { } previous
            && !string.Equals(previous.Connector, connection.Connector, StringComparison.Ordinal))
        {
            errors.Add(S["The connected data set '{0}' can only move to a connection of the same kind.", name]);

            return;
        }

        var accessMode = model.AccessMode
            ?? dataSet?.AccessMode
            ?? (descriptor.Supports(DataConnectorCapabilities.Live) ? DataSetAccessMode.Live : DataSetAccessMode.Imported);

        if (accessMode == DataSetAccessMode.Live && !descriptor.Supports(DataConnectorCapabilities.Live))
        {
            errors.Add(S["The connector of the connected data set '{0}' can't read data sets live; import it instead.", name]);
        }

        if (accessMode == DataSetAccessMode.Imported && !descriptor.Supports(DataConnectorCapabilities.Import))
        {
            errors.Add(S["The connector of the connected data set '{0}' can't import data sets; read it live instead.", name]);
        }

        var fields = model.Fields is null ? dataSet?.Fields ?? [] : NormalizeFields(name, model.Fields, errors);
        var refresh = model.Refresh ?? dataSet?.Refresh ?? new DataSetRefreshSettings();

        refresh.IncrementalField = string.IsNullOrWhiteSpace(refresh.IncrementalField) ? null : refresh.IncrementalField.Trim();

        if (accessMode == DataSetAccessMode.Imported)
        {
            ValidateRefresh(name, descriptor, refresh, fields, errors);
        }

        if (errors.Count > 0)
        {
            return;
        }

        dataSet ??= new ConnectedDataSet
        {
            ItemId = DataConnectorRecipeJson.NewItemId(model.ItemId),
            Name = name,
            CreatedUtc = model.CreatedUtc ?? _clock.UtcNow,
            Author = model.Author,
        };

        dataSet.DisplayText = displayText ?? name;
        dataSet.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        dataSet.ConnectionId = connection.ItemId;
        dataSet.Resource = string.IsNullOrWhiteSpace(model.Resource) ? dataSet.Resource : model.Resource.Trim();
        dataSet.AccessMode = accessMode;
        dataSet.IsEnabled = model.IsEnabled ?? dataSet.IsEnabled;
        dataSet.Fields = fields;
        dataSet.Refresh = refresh;

        dataSet.Properties ??= new Dictionary<string, object>();
        DataConnectorRecipeJson.MergeProperties(dataSet.Properties, model.Properties);

        // A data set imported without its fields gets them now, so it can be read at once.
        if (dataSet.Fields.Count == 0)
        {
            await TryDescribeAsync(dataSet);
        }

        var defaultDateField = model.DefaultDateField ?? dataSet.DefaultDateField;

        dataSet.DefaultDateField = dataSet.Fields.FirstOrDefault(field => field.IsIncluded && field.Name == defaultDateField && DataValues.IsTemporal(field.Type))?.Name;
        dataSet.Import ??= new DataSetImportState();
        dataSet.Import.NextRefreshUtc = refresh.IntervalMinutes is > 0 and var minutes
            ? dataSet.Import.LastRefreshUtc?.AddMinutes(minutes) ?? _clock.UtcNow
            : null;

        if (dataSet.AccessMode == DataSetAccessMode.Imported && dataSet.IsEnabled && string.IsNullOrEmpty(dataSet.Import.ActiveVersion))
        {
            dataSet.Import.RefreshRequestedUtc ??= _clock.UtcNow;
        }

        if (isNew)
        {
            await _dataSetStore.CreateAsync(dataSet);
        }
        else
        {
            dataSet.ModifiedUtc = model.ModifiedUtc ?? _clock.UtcNow;
            await _dataSetStore.UpdateAsync(dataSet);
        }
    }

    private async Task<DataConnection> FindConnectionAsync(ConnectedDataSetRecipeEntry model)
    {
        if (!string.IsNullOrWhiteSpace(model.ConnectionId) && await _connectionStore.FindByIdAsync(model.ConnectionId.Trim()) is { } connection)
        {
            return connection;
        }

        return string.IsNullOrWhiteSpace(model.ConnectionName) ? null : await _connectionStore.FindByNameAsync(model.ConnectionName.Trim());
    }

    private List<ConnectedDataField> NormalizeFields(string dataSetName, List<ConnectedDataField> entries, List<string> errors)
    {
        var fields = new List<ConnectedDataField>();

        foreach (var field in entries)
        {
            if (string.IsNullOrWhiteSpace(field?.SourceName) && string.IsNullOrWhiteSpace(field?.Name))
            {
                continue;
            }

            var sourceName = string.IsNullOrWhiteSpace(field.SourceName) ? field.Name.Trim() : field.SourceName;
            var fieldName = string.IsNullOrWhiteSpace(field.Name) ? ConnectedDataSetFields.ToFieldName(sourceName) : field.Name.Trim();

            if (!DataConnectionDisplayDriver.TechnicalName().IsMatch(fieldName))
            {
                errors.Add(S["The field name '{0}' of the connected data set '{1}' must start with a letter and hold only letters, digits and underscores.", fieldName, dataSetName]);

                continue;
            }

            if (fields.Any(existing => string.Equals(existing.Name, fieldName, StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add(S["The connected data set '{0}' has two fields named '{1}'.", dataSetName, fieldName]);

                continue;
            }

            fields.Add(new ConnectedDataField
            {
                Name = fieldName,
                SourceName = sourceName,
                DisplayName = string.IsNullOrWhiteSpace(field.DisplayName) ? fieldName : field.DisplayName.Trim(),
                Description = string.IsNullOrWhiteSpace(field.Description) ? null : field.Description.Trim(),
                Type = field.Type,
                SourceType = field.SourceType,
                IsIdentifier = field.IsIdentifier,
                IsIncluded = field.IsIncluded,
            });
        }

        return fields;
    }

    private void ValidateRefresh(string name, DataConnectorDescriptor descriptor, DataSetRefreshSettings refresh, List<ConnectedDataField> fields, List<string> errors)
    {
        if (refresh.IntervalMinutes is < 1 or > 525_600)
        {
            errors.Add(S["The refresh interval of the connected data set '{0}' must be between 1 minute and one year, or empty to refresh only on demand.", name]);
        }

        if (refresh.Mode != DataSetRefreshMode.Incremental)
        {
            return;
        }

        if (!descriptor.Supports(DataConnectorCapabilities.IncrementalImport))
        {
            errors.Add(S["The connector of the connected data set '{0}' can't refresh incrementally.", name]);

            return;
        }

        // Without fields, the first refresh reads them; the incremental settings are checked when they are known.
        if (fields.Count == 0)
        {
            return;
        }

        if (!fields.Any(field => field.IsIdentifier))
        {
            errors.Add(S["An incremental refresh of the connected data set '{0}' needs at least one identifier field, to replace the rows that changed.", name]);
        }
        else if (!fields.Any(field => field.Name == refresh.IncrementalField && (DataValues.IsTemporal(field.Type) || DataValues.IsNumeric(field.Type))))
        {
            errors.Add(S["An incremental refresh of the connected data set '{0}' needs a date or number field that grows when a row changes.", name]);
        }
    }

    private async Task TryDescribeAsync(ConnectedDataSet dataSet)
    {
        using var timeout = new CancellationTokenSource(_discoveryTimeout);

        try
        {
            var sourceFields = await _reader.DescribeAsync(dataSet, timeout.Token);

            if (sourceFields.Count > 0)
            {
                dataSet.Fields = ConnectedDataSetFields.Create(sourceFields);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || timeout.IsCancellationRequested)
        {
            // The source may not be reachable from where the recipe runs. The data set is still saved: an imported one
            // reads its fields at its first refresh, and a live one when its fields are discovered in its editor.
            _logger.LogWarning(exception, "The fields of the connected data set '{DataSet}' couldn't be read while importing it.", dataSet.Name);
        }
    }
}
