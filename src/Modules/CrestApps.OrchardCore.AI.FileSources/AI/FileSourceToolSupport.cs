using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.FileSources.AI;

/// <summary>
/// What the file source tools share: the connectors, the admin links, the FTP and SFTP connection settings and the fields
/// a tool returns.
/// </summary>
/// <remarks>
/// The FTP and SFTP connection settings belong to the FTP and SFTP connector packages, which this module does not
/// reference so a site that reads only its own folders never loads them. They are read and written by name, member by
/// member, in the shape their recipe entries carry; a test checks each name against the real types. Their passwords,
/// keys and passphrases are never read out or written: a tool reports only whether one is stored.
/// </remarks>
internal static class FileSourceToolSupport
{
    /// <summary>
    /// The name of the FTP connector.
    /// </summary>
    public const string FtpConnectorName = "Ftp";

    /// <summary>
    /// The name of the SFTP connector.
    /// </summary>
    public const string SftpConnectorName = "Sftp";

    /// <summary>
    /// The key of the FTP connection settings in a file source's properties.
    /// </summary>
    public const string FtpConnectionKey = "FtpConnectionMetadata";

    /// <summary>
    /// The key of the SFTP connection settings in a file source's properties.
    /// </summary>
    public const string SftpConnectionKey = "SftpConnectionMetadata";

    /// <summary>
    /// The FTP connection settings a tool reads and writes, as tool argument name and stored member name.
    /// </summary>
    public static readonly IReadOnlyList<(string Argument, string Member)> FtpFields =
    [
        ("host", "Host"),
        ("port", "Port"),
        ("username", "Username"),
        ("encryptionMode", "EncryptionMode"),
        ("dataConnectionType", "DataConnectionType"),
        ("validateAnyCertificate", "ValidateAnyCertificate"),
        ("connectTimeout", "ConnectTimeout"),
        ("readTimeout", "ReadTimeout"),
        ("retryAttempts", "RetryAttempts"),
    ];

    /// <summary>
    /// The SFTP connection settings a tool reads and writes, as tool argument name and stored member name.
    /// </summary>
    public static readonly IReadOnlyList<(string Argument, string Member)> SftpFields =
    [
        ("host", "Host"),
        ("port", "Port"),
        ("username", "Username"),
        ("proxyType", "ProxyType"),
        ("proxyHost", "ProxyHost"),
        ("proxyPort", "ProxyPort"),
        ("proxyUsername", "ProxyUsername"),
        ("connectionTimeout", "ConnectionTimeout"),
        ("keepAliveInterval", "KeepAliveInterval"),
    ];

    /// <summary>
    /// The stored members of the FTP connection settings that hold a secret.
    /// </summary>
    public static readonly IReadOnlyList<string> FtpSecretMembers = ["Password"];

    /// <summary>
    /// The stored members of the SFTP connection settings that hold a secret of the server login.
    /// </summary>
    public static readonly IReadOnlyList<string> SftpSecretMembers = ["Password", "PrivateKey", "Passphrase"];

    /// <summary>
    /// The stored member of the SFTP connection settings that holds the proxy's password.
    /// </summary>
    public const string SftpProxySecretMember = "ProxyPassword";

    /// <summary>
    /// Gets the connectors enabled on this site.
    /// </summary>
    public static IReadOnlyList<IngestionConnectorDescriptor> GetConnectors(IServiceProvider services)
        => services.GetService<IOptions<IngestionConnectorOptions>>()?.Value?.Connectors
            .OrderBy(connector => connector.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    /// <summary>
    /// Gets the path of a file source's editor.
    /// </summary>
    public static string GetEditPath(IServiceProvider services, string id)
        => AIModuleTools.GetAdminPath(services, FileSourceConstants.Feature.FileSources, "FileSources", "Edit", new RouteValueDictionary { ["id"] = id });

    /// <summary>
    /// Gets the path of the page that creates a file source for a connector.
    /// </summary>
    public static string GetCreatePath(IServiceProvider services, string connector)
        => AIModuleTools.GetAdminPath(services, FileSourceConstants.Feature.FileSources, "FileSources", "Create", new RouteValueDictionary { ["source"] = connector });

    /// <summary>
    /// Gets the path of the File Sources page.
    /// </summary>
    public static string GetIndexPath(IServiceProvider services)
        => AIModuleTools.GetAdminPath(services, FileSourceConstants.Feature.FileSources, "FileSources", "Index");

    /// <summary>
    /// Reads a copy of connection settings stored by name.
    /// </summary>
    /// <param name="fileSource">The file source.</param>
    /// <param name="key">The key of the settings.</param>
    /// <returns>A copy, or <see langword="null"/> when none is stored.</returns>
    public static JsonObject ReadConnection(FileSource fileSource, string key)
    {
        if (fileSource?.Properties is null || !fileSource.Properties.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            JsonObject json => (JsonObject)json.DeepClone(),
            JsonElement { ValueKind: JsonValueKind.Object } element => JsonObject.Create(element),
            _ => JsonSerializer.SerializeToNode(value, ExtensibleEntityExtensions.JsonSerializerOptions) as JsonObject,
        };
    }

    /// <summary>
    /// Gets a member of stored settings, whatever its letter case.
    /// </summary>
    public static JsonNode GetMember(JsonObject json, string member)
    {
        if (json is null)
        {
            return null;
        }

        foreach (var (name, value) in json)
        {
            if (string.Equals(name, member, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets a member of stored settings, replacing it whatever its letter case.
    /// </summary>
    public static void SetMember(JsonObject json, string member, JsonNode value)
    {
        foreach (var name in json.Select(entry => entry.Key).Where(name => string.Equals(name, member, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            json.Remove(name);
        }

        json[member] = value;
    }

    /// <summary>
    /// Whether stored settings hold a secret in a member.
    /// </summary>
    public static bool HasSecret(JsonObject json, string member)
        => GetMember(json, member) is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text);

    /// <summary>
    /// Gets a text member of stored settings.
    /// </summary>
    public static string GetText(JsonObject json, string member)
        => GetMember(json, member) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>
    /// Describes a file source from an explicit list of fields. No password, key or passphrase is returned.
    /// </summary>
    public static async Task<object> DescribeAsync(IServiceProvider services, FileSource fileSource, CancellationToken cancellationToken)
    {
        var dataSource = string.IsNullOrEmpty(fileSource.AIDataSourceId)
            ? null
            : await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(fileSource.AIDataSourceId, cancellationToken);
        var localClock = services.GetService<ILocalClock>();
        var settings = fileSource.TryGet<FileSourceMetadata>(out var metadata) ? metadata : null;
        var fileSystem = fileSource.TryGet<FileSystemFileSourceMetadata>(out var folder) ? folder : null;
        var remote = fileSource.TryGet<RemoteFileSourceMetadata>(out var remoteFolder) ? remoteFolder : null;
        var lastRun = fileSource.TryGet<FileSourceRunSummary>(out var summary) && summary.StartedUtc != default ? summary : null;

        return new
        {
            id = fileSource.ItemId,
            displayText = fileSource.DisplayText,
            connector = fileSource.Source,
            dataSource = new
            {
                id = fileSource.AIDataSourceId,
                displayText = dataSource?.DisplayText,
                found = dataSource is not null,
            },
            enabled = fileSource.Enabled,
            reindexIntervalMinutes = fileSource.ReindexIntervalMinutes,
            settings = settings is null
                ? null
                : new
                {
                    figureMode = settings.FigureMode.ToString(),
                    visionDeploymentName = settings.VisionDeploymentName,
                    utilityDeploymentName = settings.UtilityDeploymentName,
                    maxFigureDescriptionsPerDocument = settings.MaxFigureDescriptionsPerDocument,
                    maxItemsPerRun = settings.MaxItemsPerRun,
                    language = settings.Language,
                },
            fileSystem = fileSystem is null
                ? null
                : new
                {
                    rootPath = fileSystem.RootPath,
                    recursive = fileSystem.Recursive,
                    maxItems = fileSystem.MaxItems,
                },
            remote = remote is null
                ? null
                : new
                {
                    rootPath = remote.RootPath,
                    recursive = remote.Recursive,
                    maxItems = remote.MaxItems,
                },
            ftp = DescribeConnection(ReadConnection(fileSource, FtpConnectionKey), FtpFields, FtpSecretMembers, null),
            sftp = DescribeConnection(ReadConnection(fileSource, SftpConnectionKey), SftpFields, SftpSecretMembers, SftpProxySecretMember),
            lastRun = lastRun is null
                ? null
                : new
                {
                    startedAt = await AIModuleTools.ToLocalAsync(localClock, lastRun.StartedUtc),
                    completedAt = await AIModuleTools.ToLocalAsync(localClock, lastRun.CompletedUtc),
                    status = lastRun.Status.ToString(),
                    discoveryCompleted = lastRun.DiscoveryCompleted,
                    discovered = lastRun.ItemsDiscovered,
                    indexed = lastRun.ItemsIndexed,
                    skipped = lastRun.ItemsSkipped,
                    removed = lastRun.ItemsDeleted,
                    failed = lastRun.ItemsFailed,
                    error = lastRun.Error,
                },
            createdAt = await AIModuleTools.ToLocalAsync(localClock, fileSource.CreatedUtc),
            modifiedAt = await AIModuleTools.ToLocalAsync(localClock, fileSource.ModifiedUtc),
            author = fileSource.Author,
            editUrl = GetEditPath(services, fileSource.ItemId),
        };
    }

    private static Dictionary<string, object> DescribeConnection(JsonObject connection, IReadOnlyList<(string Argument, string Member)> fields, IReadOnlyList<string> secretMembers, string proxySecretMember)
    {
        if (connection is null)
        {
            return null;
        }

        var result = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var (argument, member) in fields)
        {
            if (GetMember(connection, member) is JsonValue value)
            {
                result[argument] = value.DeepClone();
            }
        }

        foreach (var member in secretMembers)
        {
            result["has" + member] = HasSecret(connection, member);
        }

        if (proxySecretMember is not null)
        {
            result["has" + proxySecretMember] = HasSecret(connection, proxySecretMember);
        }

        return result;
    }
}
