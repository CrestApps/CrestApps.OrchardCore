using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.DataSources;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Creates or updates a connected data set with the data set editor's checks. As the editor does, a data set saved
/// without fields gets them from its source first, and a new imported data set has its first import requested when the
/// caller may refresh data sets. The connector's own settings of a data set (an HTTP request, a query) are set on its
/// page.
/// </summary>
public sealed class SaveConnectedDataSetTool : DataConnectorAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveConnectedDataSet";

    private static readonly TimeSpan _discoveryTimeout = TimeSpan.FromSeconds(20);

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the data set to update. Leave it out to create a data set."
        },
        "connectionId": {
          "type": "string",
          "description": "The ID of the connection a new data set reads, from listDataConnections. It can't be changed later."
        },
        "displayText": {
          "type": "string",
          "description": "The data set's name. Required for a new data set."
        },
        "name": {
          "type": "string",
          "description": "The technical name of a new data set: a letter, then letters, digits and underscores. Made from the name when left out. Reports, pipelines and the read permission use it, so it can't be changed later."
        },
        "description": {
          "type": "string",
          "description": "What the data set holds. An empty text clears it."
        },
        "resource": {
          "type": "string",
          "description": "What the data set reads on its connection, such as a table or view from listDataConnectionResources, an index, an API path or a media file path."
        },
        "accessMode": {
          "type": "string",
          "enum": ["Live", "Imported"],
          "description": "Live reads the source every time; Imported copies the rows into the site's database on a schedule. Defaults to Live when the connector can read live."
        },
        "isEnabled": {
          "type": "boolean",
          "description": "Whether reports and data pipelines can read the data set. Defaults to true for a new data set."
        },
        "fields": {
          "type": "array",
          "description": "The fields, replacing the current ones. Leave it out to keep them; a data set without fields reads them from its source.",
          "items": {
            "type": "object",
            "properties": {
              "sourceName": { "type": "string", "description": "The field's name at the source." },
              "name": { "type": "string", "description": "The field's technical name. Made from sourceName when left out." },
              "displayName": { "type": "string", "description": "The field's display name." },
              "type": { "type": "string", "enum": ["Text", "Integer", "Decimal", "Boolean", "Date", "DateTime"], "description": "The field's type." },
              "isIdentifier": { "type": "boolean", "description": "Whether the field identifies a row; an incremental refresh needs one." },
              "isIncluded": { "type": "boolean", "description": "Whether reports and pipelines see the field. Defaults to true." }
            },
            "required": ["sourceName"],
            "additionalProperties": false
          }
        },
        "defaultDateField": {
          "type": "string",
          "description": "The technical name of the date field reports filter on by default."
        },
        "refreshIntervalMinutes": {
          "type": "integer",
          "description": "Imported data sets: minutes between refreshes, from 1 to 525600. Give 0 to refresh only on demand."
        },
        "refreshMode": {
          "type": "string",
          "enum": ["Full", "Incremental"],
          "description": "Imported data sets: Full reads every row; Incremental reads only rows whose incrementalField grew."
        },
        "incrementalField": {
          "type": "string",
          "description": "Imported data sets with an incremental refresh: a date or number field that grows when a row changes."
        },
        "schemaChangePolicy": {
          "type": "string",
          "enum": ["Accept", "Fail"],
          "description": "Imported data sets: whether a refresh accepts a change of the source's fields or fails."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a data set on a connection, or updates one by id: resource, live or imported, fields, default date field and refresh schedule. Fields are read from the source when none are given, and a new imported data set starts its first import when the user may refresh data sets. The connector's own settings (HTTP request, query) are set on the data set's page.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission RequiredPermission => DataConnectorPermissions.ManageConnectedDataSets;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var dataSetStore = services.GetRequiredService<IConnectedDataSetStore>();
        var connectionStore = services.GetRequiredService<IDataConnectionStore>();
        var connectorManager = services.GetRequiredService<IDataConnectorManager>();
        var clock = services.GetRequiredService<IClock>();

        var id = ReadString(arguments, "id");
        var connectionId = ReadString(arguments, "connectionId");
        var isNew = id is null;
        ConnectedDataSet dataSet;
        DataConnection connection;

        if (isNew)
        {
            if (connectionId is null)
            {
                return "Nothing was saved: give the ID of the connection the new data set reads in connectionId.";
            }

            connection = await connectionStore.FindByIdAsync(connectionId, cancellationToken);

            if (connection is null)
            {
                return $"Nothing was saved: no data connection has the ID '{connectionId}'. Call listDataConnections to find it.";
            }

            var newDescriptor = connectorManager.GetDescriptor(connection.Connector);

            if (newDescriptor is null)
            {
                return $"Nothing was saved: the connector '{connection.Connector}' of the connection '{connection.DisplayText}' isn't enabled.";
            }

            dataSet = new ConnectedDataSet
            {
                ItemId = IdGenerator.GenerateId(),
                ConnectionId = connection.ItemId,
                AccessMode = newDescriptor.Supports(DataConnectorCapabilities.Live) ? DataSetAccessMode.Live : DataSetAccessMode.Imported,
                CreatedUtc = clock.UtcNow,
                Author = user.Identity?.Name,
            };
        }
        else
        {
            dataSet = await dataSetStore.FindByIdAsync(id, cancellationToken);

            if (dataSet is null)
            {
                return $"Nothing was saved: no connected data set has the ID '{id}'. Call listConnectedDataSets to find it.";
            }

            if (connectionId is not null && !string.Equals(connectionId, dataSet.ConnectionId, StringComparison.Ordinal))
            {
                return "Nothing was saved: a data set's connection can't be changed. Create a data set on the other connection instead.";
            }

            var newName = ReadString(arguments, "name");

            if (newName is not null && !string.Equals(newName, dataSet.Name, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: the data set '{dataSet.Name}' can't be renamed to '{newName}', because reports, data pipelines and its read permission use its technical name. Change displayText instead.";
            }

            connection = string.IsNullOrEmpty(dataSet.ConnectionId) ? null : await connectionStore.FindByIdAsync(dataSet.ConnectionId, cancellationToken);
        }

        var model = ToModel(dataSet);
        var refusal = Merge(arguments, model);

        if (refusal is not null)
        {
            return refusal;
        }

        var descriptor = connection is null ? null : connectorManager.GetDescriptor(connection.Connector);
        var errors = new List<string>();

        await services.GetRequiredService<ConnectedDataSetEditor>().ApplyAsync(dataSet, model, descriptor, isNew, (_, message) => errors.Add(message));

        // A data set saved before its fields were read gets them now, so it can be read at once, as in the editor.
        if (errors.Count == 0 && dataSet.Fields.Count == 0)
        {
            var problem = await DiscoverFieldsAsync(services, dataSet, cancellationToken);

            if (problem is not null)
            {
                errors.Add(problem);
            }
        }

        if (errors.Count > 0)
        {
            return Json(new
            {
                saved = false,
                errors,
            });
        }

        var importNow = dataSet.AccessMode == DataSetAccessMode.Imported && (isNew || string.IsNullOrEmpty(dataSet.Import?.ActiveVersion));

        if (isNew)
        {
            await dataSetStore.CreateAsync(dataSet, cancellationToken);
        }
        else
        {
            dataSet.ModifiedUtc = clock.UtcNow;
            await dataSetStore.UpdateAsync(dataSet, cancellationToken);
        }

        var importRequested = false;

        if (importNow && await IsAuthorizedAsync(services, user, DataConnectorPermissions.RefreshConnectedDataSets))
        {
            await services.GetRequiredService<DataConnectorCommands>().RequestRefreshAsync(dataSet, cancellationToken);
            importRequested = true;
        }

        return Json(new
        {
            saved = true,
            created = isNew,
            dataSet = DataConnectorToolDescriptions.DescribeDataSetRow(
                dataSet,
                connection,
                await ToLocalAsync(services, dataSet.Import?.LastRefreshUtc),
                await ToLocalAsync(services, dataSet.Import?.NextRefreshUtc)),
            fields = dataSet.Fields.Select(field => new
            {
                name = field.Name,
                sourceName = field.SourceName,
                type = field.Type.ToString(),
                isIdentifier = field.IsIdentifier,
                isIncluded = field.IsIncluded,
            }).ToArray(),
            importRequested,
            message = importRequested
                ? "The data set was saved, and its first import was requested: the refresh task starts it within about a minute. Call getConnectedDataSet with includeRuns later to see its result."
                : importNow
                    ? "The data set was saved. Its first import was not started, because the user may not refresh data sets."
                    : "The data set was saved.",
            editPath = GetAdminPath(services, "ConnectedDataSets", "Edit", dataSet.ItemId),
        });
    }

    private static ConnectedDataSetFieldsViewModel ToModel(ConnectedDataSet dataSet)
        => new()
        {
            DisplayText = dataSet.DisplayText,
            Name = dataSet.Name,
            Description = dataSet.Description,
            Resource = dataSet.Resource,
            AccessMode = dataSet.AccessMode,
            IsEnabled = dataSet.IsEnabled,
            DefaultDateField = dataSet.DefaultDateField,
            RefreshIntervalMinutes = dataSet.Refresh?.IntervalMinutes,
            RefreshMode = dataSet.Refresh?.Mode ?? DataSetRefreshMode.Full,
            IncrementalField = dataSet.Refresh?.IncrementalField,
            SchemaChangePolicy = dataSet.Refresh?.SchemaChangePolicy ?? DataSetSchemaChangePolicy.Accept,
            Fields =
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
            ],
        };

    private static string Merge(AIFunctionArguments arguments, ConnectedDataSetFieldsViewModel model)
    {
        if (!TryReadEnum<DataSetAccessMode>(arguments, "accessMode", out var accessMode)
            || !TryReadEnum<DataSetRefreshMode>(arguments, "refreshMode", out var refreshMode)
            || !TryReadEnum<DataSetSchemaChangePolicy>(arguments, "schemaChangePolicy", out var schemaChangePolicy))
        {
            return "Nothing was saved: accessMode, refreshMode and schemaChangePolicy take one of the values the tool's schema lists.";
        }

        if (!TryReadBool(arguments, "isEnabled", out var isEnabled))
        {
            return "Nothing was saved: isEnabled must be true or false.";
        }

        if (!TryReadInt(arguments, "refreshIntervalMinutes", out var interval))
        {
            return "Nothing was saved: refreshIntervalMinutes must be a whole number of minutes.";
        }

        if (!TryReadArray(arguments, "fields", out var fields))
        {
            return "Nothing was saved: fields must be a list of fields.";
        }

        model.DisplayText = ReadString(arguments, "displayText") ?? model.DisplayText;
        model.Name = ReadString(arguments, "name") ?? model.Name;
        model.Description = Has(arguments, "description") ? ReadString(arguments, "description") : model.Description;
        model.Resource = Has(arguments, "resource") ? ReadString(arguments, "resource") : model.Resource;
        model.AccessMode = accessMode ?? model.AccessMode;
        model.IsEnabled = isEnabled ?? model.IsEnabled;
        model.DefaultDateField = Has(arguments, "defaultDateField") ? ReadString(arguments, "defaultDateField") : model.DefaultDateField;
        model.RefreshIntervalMinutes = interval is null ? model.RefreshIntervalMinutes : interval == 0 ? null : interval;
        model.RefreshMode = refreshMode ?? model.RefreshMode;
        model.IncrementalField = Has(arguments, "incrementalField") ? ReadString(arguments, "incrementalField") : model.IncrementalField;
        model.SchemaChangePolicy = schemaChangePolicy ?? model.SchemaChangePolicy;

        if (fields is null)
        {
            return null;
        }

        var entries = new List<ConnectedDataFieldViewModel>();

        foreach (var node in fields)
        {
            if (node is not JsonObject field)
            {
                return "Nothing was saved: each field must be an object with at least a sourceName.";
            }

            var typeText = Text(field, "type");
            var type = DataFieldType.Text;

            if (typeText is not null && (int.TryParse(typeText, out _) || !Enum.TryParse(typeText, ignoreCase: true, out type)))
            {
                return $"Nothing was saved: '{typeText}' is not a field type. Use one of: {string.Join(", ", Enum.GetNames<DataFieldType>())}.";
            }

            entries.Add(new ConnectedDataFieldViewModel
            {
                SourceName = Text(field, "sourceName"),
                Name = Text(field, "name"),
                DisplayName = Text(field, "displayName"),
                Type = type,
                SourceType = type,
                IsIdentifier = Flag(field, "isIdentifier") ?? false,
                IsIncluded = Flag(field, "isIncluded") ?? true,
            });
        }

        // A field the source described before keeps its source type.
        foreach (var entry in entries)
        {
            var known = model.Fields.FirstOrDefault(field => string.Equals(field.SourceName, entry.SourceName, StringComparison.Ordinal));

            if (known is not null)
            {
                entry.SourceType = known.SourceType;
            }
        }

        model.Fields = entries;

        return null;
    }

    private static async Task<string> DiscoverFieldsAsync(IServiceProvider services, ConnectedDataSet dataSet, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_discoveryTimeout);

        try
        {
            var sourceFields = await services.GetRequiredService<ConnectedDataReader>().DescribeAsync(dataSet, timeout.Token);

            if (sourceFields.Count == 0)
            {
                return "The source described no fields. Check the resource and its settings.";
            }

            dataSet.Fields = ConnectedDataSetFields.Create(sourceFields);

            return null;
        }
        catch (DataConnectorException exception)
        {
            return $"The fields couldn't be read: {exception.Message}";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return $"The fields couldn't be read within {_discoveryTimeout.TotalSeconds} seconds.";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<SaveConnectedDataSetTool>>().LogWarning(exception, "The fields of the data set '{DataSet}' couldn't be read.", dataSet.Name);

            return "The fields couldn't be read. See the site's log for details.";
        }
    }

    private static string Text(JsonObject json, string name)
        => json.TryGetPropertyValue(name, out var node) && node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text.Trim()
            : null;

    private static bool? Flag(JsonObject json, string name)
    {
        if (!json.TryGetPropertyValue(name, out var node) || node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        return value.TryGetValue<string>(out var text) && bool.TryParse(text, out flag) ? flag : null;
    }
}
