using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Connectors.Elasticsearch;
using CrestApps.OrchardCore.DataConnectors.Connectors.Files;
using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Relational;
using CrestApps.OrchardCore.DataConnectors.Services;

namespace CrestApps.OrchardCore.DataConnectors.AI;

/// <summary>
/// Builds what the data connector tools return about connections and data sets, from an explicit list of fields: a
/// secret is reported only as whether it is stored, and a header only by its name.
/// </summary>
internal static class DataConnectorToolDescriptions
{
    /// <summary>
    /// The name the SQL Server connector stores its settings under. The SQL Server module's settings type can't be used
    /// here, so its non-secret members are read by name.
    /// </summary>
    public const string SqlServerSettingsKey = "SqlServerConnectionSettings";

    /// <summary>
    /// The name of the SQL Server connector's secret, its password or client secret.
    /// </summary>
    public const string SqlServerPasswordSecret = "Password";

    /// <summary>
    /// The members of the SQL Server settings a tool reports.
    /// </summary>
    public static readonly string[] SqlServerFields =
    [
        "Server",
        "Database",
        "Authentication",
        "UserName",
        "Encrypt",
        "TrustServerCertificate",
        "ReadOnlyIntent",
        "ConnectTimeoutSeconds",
    ];

    /// <summary>
    /// The SQL Server sign-in modes, in their declared order, so a mode stored as a number reads as its name.
    /// </summary>
    public static readonly string[] SqlServerAuthentications =
    [
        "SqlLogin",
        "Integrated",
        "EntraServicePrincipal",
        "EntraManagedIdentity",
        "EntraDefault",
    ];

    /// <summary>
    /// The SQL Server sign-in modes that use the identity of the site's host instead of a credential of the connection
    /// (review finding SEC-A8).
    /// </summary>
    public static readonly string[] SqlServerHostIdentityAuthentications =
    [
        "Integrated",
        "EntraManagedIdentity",
        "EntraDefault",
    ];

    /// <summary>
    /// The connectors whose connections a tool can create and change: their settings types are known to this module.
    /// </summary>
    public static readonly string[] EditableConnectors =
    [
        DataConnectorConstants.Connectors.HttpApi,
        DataConnectorConstants.Connectors.Elasticsearch,
        DataConnectorConstants.Connectors.MediaFiles,
    ];

    /// <summary>
    /// The secrets an HTTP-based connection may store.
    /// </summary>
    public static readonly string[] HttpSecrets =
    [
        HttpAuthenticationSecrets.ApiKey,
        HttpAuthenticationSecrets.Password,
        HttpAuthenticationSecrets.BearerToken,
        HttpAuthenticationSecrets.ClientSecret,
    ];

    /// <summary>
    /// Whether a connection reads an HTTP API or Elasticsearch.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns><see langword="true"/> for an HTTP-based connection.</returns>
    public static bool IsHttp(DataConnection connection)
        => string.Equals(connection.Connector, DataConnectorConstants.Connectors.HttpApi, StringComparison.OrdinalIgnoreCase)
            || string.Equals(connection.Connector, DataConnectorConstants.Connectors.Elasticsearch, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a connection reads media library files.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns><see langword="true"/> for a media files connection.</returns>
    public static bool IsMediaFiles(DataConnection connection)
        => string.Equals(connection.Connector, DataConnectorConstants.Connectors.MediaFiles, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a connection reads SQL Server.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns><see langword="true"/> for a SQL Server connection.</returns>
    public static bool IsSqlServer(DataConnection connection)
        => string.Equals(connection.Connector, DataConnectorConstants.Connectors.SqlServer, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Describes a connector.
    /// </summary>
    /// <param name="descriptor">The connector.</param>
    /// <returns>The description.</returns>
    public static object DescribeConnector(DataConnectorDescriptor descriptor)
        => new
        {
            name = descriptor.Name,
            displayName = descriptor.DisplayName.Value,
            description = descriptor.Description.Value,
            live = descriptor.Supports(DataConnectorCapabilities.Live),
            import = descriptor.Supports(DataConnectorCapabilities.Import),
            incrementalImport = descriptor.Supports(DataConnectorCapabilities.IncrementalImport),
            listsResources = descriptor.Supports(DataConnectorCapabilities.Discovery),
            toolCanCreate = EditableConnectors.Contains(descriptor.Name, StringComparer.OrdinalIgnoreCase),
        };

    /// <summary>
    /// Describes a connection with its non-secret settings.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="descriptor">Its connector, or <see langword="null"/> when the connector isn't enabled.</param>
    /// <param name="secretManager">The secrets of connections.</param>
    /// <param name="created">The local creation time.</param>
    /// <param name="modified">The local time of the last change.</param>
    /// <param name="editPath">The connection's admin page.</param>
    /// <returns>The description.</returns>
    public static object DescribeConnection(
        DataConnection connection,
        DataConnectorDescriptor descriptor,
        IDataConnectionSecretManager secretManager,
        string created,
        string modified,
        string editPath)
        => new
        {
            id = connection.ItemId,
            name = connection.Name,
            displayText = connection.DisplayText,
            description = connection.Description,
            connector = connection.Connector,
            connectorDisplayName = descriptor?.DisplayName.Value,
            connectorEnabled = descriptor is not null,
            settings = DescribeSettings(connection),
            credentials = DescribeCredentials(connection, secretManager),
            created,
            modified,
            author = connection.Author,
            editPath,
        };

    /// <summary>
    /// Reads the non-secret members of a SQL Server connection's settings.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The members by name, with the sign-in mode as its name; empty when none are stored.</returns>
    public static Dictionary<string, object> ReadSqlServerSettings(DataConnection connection)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        if (connection.Properties is null
            || !connection.Properties.TryGetValue(SqlServerSettingsKey, out var stored)
            || stored is null)
        {
            return result;
        }

        JsonObject json;

        try
        {
            json = (stored as JsonNode ?? JsonSerializer.SerializeToNode(stored)) as JsonObject;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            return result;
        }

        if (json is null)
        {
            return result;
        }

        foreach (var field in SqlServerFields)
        {
            var node = json.FirstOrDefault(member => string.Equals(member.Key, field, StringComparison.OrdinalIgnoreCase)).Value;

            if (node is not JsonValue value)
            {
                continue;
            }

            var key = char.ToLowerInvariant(field[0]) + field[1..];

            if (field == "Authentication")
            {
                result[key] = value.TryGetValue<int>(out var index) && index >= 0 && index < SqlServerAuthentications.Length
                    ? SqlServerAuthentications[index]
                    : value.TryGetValue<string>(out var text) ? text : null;
            }
            else if (value.TryGetValue<bool>(out var flag))
            {
                result[key] = flag;
            }
            else if (value.TryGetValue<int>(out var number))
            {
                result[key] = number;
            }
            else if (value.TryGetValue<string>(out var text))
            {
                result[key] = text;
            }
        }

        return result;
    }

    /// <summary>
    /// Describes a connected data set in a list row.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="connection">Its connection, or <see langword="null"/> when it is gone.</param>
    /// <param name="lastRefresh">The local time of the last refresh.</param>
    /// <param name="nextRefresh">The local time of the next scheduled refresh.</param>
    /// <returns>The description.</returns>
    public static object DescribeDataSetRow(ConnectedDataSet dataSet, DataConnection connection, string lastRefresh, string nextRefresh)
    {
        var imported = dataSet.AccessMode == DataSetAccessMode.Imported;

        return new
        {
            id = dataSet.ItemId,
            name = dataSet.Name,
            displayText = dataSet.DisplayText,
            connection = new
            {
                id = dataSet.ConnectionId,
                displayText = connection?.DisplayText,
                connector = connection?.Connector,
                missing = connection is null,
            },
            resource = dataSet.Resource,
            accessMode = dataSet.AccessMode.ToString(),
            isEnabled = dataSet.IsEnabled,
            fieldCount = dataSet.Fields?.Count ?? 0,
            rowCount = imported ? dataSet.Import?.RowCount : null,
            lastStatus = imported ? dataSet.Import?.LastStatus?.ToString() : null,
            lastRefresh = imported ? lastRefresh : null,
            nextRefresh = imported ? nextRefresh : null,
            lastError = imported ? Truncate(dataSet.Import?.LastError, 300) : null,
        };
    }

    /// <summary>
    /// Describes the connector's own settings of a data set, from an explicit list of fields. The body of an HTTP request
    /// is reported only as whether there is one.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="connection">Its connection, or <see langword="null"/>.</param>
    /// <returns>The description, or <see langword="null"/> when the connector's settings aren't known here.</returns>
    public static object DescribeConnectorSettings(ConnectedDataSet dataSet, DataConnection connection)
    {
        switch (connection?.Connector)
        {
            case DataConnectorConstants.Connectors.HttpApi when dataSet.TryGet<HttpApiDataSetSettings>(out var http):
                var pagination = http.Pagination ?? new HttpPaginationSettings();

                return new
                {
                    method = http.Method,
                    hasBody = !string.IsNullOrWhiteSpace(http.Body),
                    recordsPath = http.RecordsPath,
                    format = http.Format,
                    pagination = new
                    {
                        type = pagination.Type.ToString(),
                        pageParameter = pagination.PageParameter,
                        firstPage = pagination.FirstPage,
                        offsetParameter = pagination.OffsetParameter,
                        sizeParameter = pagination.SizeParameter,
                        pageSize = pagination.PageSize,
                        cursorParameter = pagination.CursorParameter,
                        nextPath = pagination.NextPath,
                        maxPages = pagination.MaxPages,
                    },
                };

            case DataConnectorConstants.Connectors.Elasticsearch when dataSet.TryGet<ElasticsearchDataSetSettings>(out var elasticsearch):
                return new
                {
                    query = elasticsearch.Query,
                    scrollMinutes = elasticsearch.ScrollMinutes,
                };

            case DataConnectorConstants.Connectors.SqlServer when dataSet.TryGet<RelationalDataSetSettings>(out var relational):
                return new
                {
                    query = relational.Query,
                    commandTimeoutSeconds = relational.CommandTimeoutSeconds,
                };

            case DataConnectorConstants.Connectors.MediaFiles when dataSet.TryGet<MediaFileDataSetSettings>(out var file):
                return new
                {
                    delimiter = file.Delimiter,
                    hasHeaderRow = file.HasHeaderRow,
                    sheetName = file.SheetName,
                };

            default:
                return null;
        }
    }

    /// <summary>
    /// Cuts a text to a length.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="length">The longest length kept.</param>
    /// <returns>The text, cut with an ellipsis when longer.</returns>
    public static string Truncate(string value, int length)
        => value is null || value.Length <= length ? value : string.Concat(value.AsSpan(0, length), "...");

    private static object DescribeSettings(DataConnection connection)
    {
        if (IsHttp(connection))
        {
            var settings = connection.GetOrCreate<HttpConnectionSettings>();
            var authentication = settings.Authentication ?? new HttpAuthenticationSettings();

            return new
            {
                baseUrl = settings.BaseUrl,
                timeoutSeconds = settings.TimeoutSeconds,

                // A header's value often carries a credential, so only its name is reported.
                headerNames = (settings.Headers ?? []).Select(header => header.Name).ToArray(),
                authentication = new
                {
                    type = authentication.Type.ToString(),
                    apiKeyName = authentication.ApiKeyName,
                    apiKeyLocation = authentication.ApiKeyLocation.ToString(),
                    apiKeyPrefix = authentication.ApiKeyPrefix,
                    userName = authentication.UserName,
                    tokenUrl = authentication.TokenUrl,
                    authority = authentication.Authority,
                    clientId = authentication.ClientId,
                    scope = authentication.Scope,
                    audience = authentication.Audience,
                    grantType = authentication.GrantType.ToString(),
                    clientAuthentication = authentication.ClientAuthentication.ToString(),
                },
            };
        }

        if (IsMediaFiles(connection))
        {
            return new
            {
                folder = connection.GetOrCreate<MediaFilesConnectionSettings>().Folder,
            };
        }

        if (IsSqlServer(connection))
        {
            var settings = ReadSqlServerSettings(connection);
            var authentication = settings.GetValueOrDefault("authentication") as string;

            return new
            {
                server = settings.GetValueOrDefault("server"),
                database = settings.GetValueOrDefault("database"),
                authentication,
                userName = settings.GetValueOrDefault("userName"),
                encrypt = settings.GetValueOrDefault("encrypt"),
                trustServerCertificate = settings.GetValueOrDefault("trustServerCertificate"),
                readOnlyIntent = settings.GetValueOrDefault("readOnlyIntent"),
                connectTimeoutSeconds = settings.GetValueOrDefault("connectTimeoutSeconds"),
                usesHostIdentity = authentication is not null && SqlServerHostIdentityAuthentications.Contains(authentication, StringComparer.OrdinalIgnoreCase),
            };
        }

        return null;
    }

    private static object DescribeCredentials(DataConnection connection, IDataConnectionSecretManager secretManager)
    {
        if (IsHttp(connection))
        {
            return new
            {
                hasApiKey = secretManager.Has(connection, HttpAuthenticationSecrets.ApiKey),
                hasPassword = secretManager.Has(connection, HttpAuthenticationSecrets.Password),
                hasBearerToken = secretManager.Has(connection, HttpAuthenticationSecrets.BearerToken),
                hasClientSecret = secretManager.Has(connection, HttpAuthenticationSecrets.ClientSecret),
            };
        }

        if (IsSqlServer(connection))
        {
            return new
            {
                hasPassword = secretManager.Has(connection, SqlServerPasswordSecret),
            };
        }

        return new
        {
            storedCredentials = connection.Secrets?.Count(secret => !string.IsNullOrEmpty(secret.Value)) ?? 0,
        };
    }
}
