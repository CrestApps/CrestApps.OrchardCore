using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.FileSources.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.FileSources.AI;

/// <summary>
/// Creates a file source, or updates one by ID, with the checks of the file source editors and the file source catalog.
/// No password, key or passphrase is taken: a person enters it on the file source's page. Saving queues a full
/// synchronization of the data source, so the tool asks for approval.
/// </summary>
public sealed class SaveAIFileSourceTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveAIFileSource";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "string",
          "description": "The ID of the file source to update, from listAIFileSources. Leave it out to create one."
        },
        "connector": {
          "type": "string",
          "description": "When creating, the connector, from the connectors listAIFileSources returns: FileSystem (this tenant's own folder) or Ftp. An SFTP file source needs a password or a private key, so it is created on its admin page. A file source's connector cannot change."
        },
        "displayText": { "type": "string", "description": "The file source's name. Required when creating." },
        "aiDataSourceId": { "type": "string", "description": "The ID of the File AI data source the files go to, from listAIDataSources. Required when creating." },
        "enabled": { "type": "boolean", "description": "Whether the file source is read on its schedule. New file sources are enabled." },
        "reindexIntervalMinutes": { "type": "integer", "minimum": 1, "description": "How often the folder is read again, in minutes. Leave it out to keep the current value; a new file source uses the site default." },
        "figureMode": { "type": "string", "enum": ["Off", "Auto", "All"], "description": "Whether figures, charts and tables in the files are described with a vision model." },
        "visionDeploymentName": { "type": "string", "description": "The deployment that describes figures; it must accept images. An empty text uses the site's." },
        "utilityDeploymentName": { "type": "string", "description": "The deployment for utility work. An empty text uses the site's." },
        "maxFigureDescriptionsPerDocument": { "type": "integer", "minimum": 0, "description": "The most figures described per document." },
        "maxItemsPerRun": { "type": "integer", "minimum": 1, "description": "The most files one run reads." },
        "language": { "type": "string", "description": "The language of the files, such as en." },
        "fileSystem": {
          "type": "object",
          "description": "The settings of the FileSystem connector. Only the given members change.",
          "properties": {
            "rootPath": { "type": "string", "description": "The folder to read, inside this tenant's file-sources folder. An empty text is that folder itself." },
            "recursive": { "type": "boolean", "description": "Whether sub-folders are read too." },
            "maxItems": { "type": "integer", "minimum": 1, "description": "The most files listed." }
          },
          "additionalProperties": false
        },
        "remote": {
          "type": "object",
          "description": "The folder of an FTP or SFTP file source. Only the given members change.",
          "properties": {
            "rootPath": { "type": "string", "description": "The folder on the server. An empty text is /." },
            "recursive": { "type": "boolean", "description": "Whether sub-folders are read too." },
            "maxItems": { "type": "integer", "minimum": 1, "description": "The most files listed." }
          },
          "additionalProperties": false
        },
        "ftp": {
          "type": "object",
          "description": "The connection of an FTP file source, without its password. Only the given members change.",
          "properties": {
            "host": { "type": "string", "description": "The FTP server's host name. Required." },
            "port": { "type": "integer", "minimum": 1, "maximum": 65535, "description": "The port." },
            "username": { "type": "string", "description": "The user name. The password is entered on the file source's page." },
            "encryptionMode": { "type": "string", "enum": ["", "None", "Explicit", "Implicit"], "description": "The encryption: empty for automatic, None for plain FTP, Explicit or Implicit FTPS." },
            "dataConnectionType": { "type": "string", "enum": ["", "AutoPassive", "AutoActive"], "description": "The data connection: empty for automatic, AutoPassive or AutoActive." },
            "validateAnyCertificate": { "type": "boolean", "description": "Whether any server certificate is accepted. Leave it false unless the user asks." },
            "connectTimeout": { "type": "integer", "description": "The connect timeout, in milliseconds." },
            "readTimeout": { "type": "integer", "description": "The read timeout, in milliseconds." },
            "retryAttempts": { "type": "integer", "description": "How many times a failed operation is tried again." }
          },
          "additionalProperties": false
        },
        "sftp": {
          "type": "object",
          "description": "The connection of an existing SFTP file source, without its password, private key or passphrase. Only the given members change.",
          "properties": {
            "host": { "type": "string", "description": "The SFTP server's host name." },
            "port": { "type": "integer", "minimum": 1, "maximum": 65535, "description": "The port." },
            "username": { "type": "string", "description": "The user name." },
            "proxyType": { "type": "string", "enum": ["", "Http", "Socks4", "Socks5"], "description": "The proxy: empty for none." },
            "proxyHost": { "type": "string", "description": "The proxy's host name." },
            "proxyPort": { "type": "integer", "minimum": 1, "maximum": 65535, "description": "The proxy's port." },
            "proxyUsername": { "type": "string", "description": "The proxy's user name." },
            "connectionTimeout": { "type": "integer", "description": "The connection timeout, in seconds." },
            "keepAliveInterval": { "type": "integer", "description": "The keep-alive interval, in seconds." }
          },
          "additionalProperties": false
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a file source for a connector (this tenant's folder or FTP), or updates one by id: name, File AI data source, schedule, figure settings and connector settings. It needs an existing File AI data source; a file system path can only be inside the tenant's own file-sources folder. Passwords and keys are never taken: the result links to the page where a person enters them. Saving queues a full synchronization of the data source, which spends embedding tokens in the background.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        if (!await AIModuleTools.IsAuthorizedAsync(services, FileSourcePermissions.ManageFileSources))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var manager = services.GetRequiredService<ISourceCatalogManager<FileSource>>();
        var id = AIModuleTools.ReadString(arguments, "id");
        FileSource existing = null;

        if (id is not null)
        {
            existing = await manager.FindByIdAsync(id, cancellationToken);

            if (existing is null)
            {
                return $"Nothing was saved: no file source has the ID '{id}'. Call listAIFileSources to find it.";
            }
        }

        var secret = AIModuleTools.FindSecretArgument(arguments);

        if (secret is not null)
        {
            var page = existing is null ? FileSourceToolSupport.GetIndexPath(services) : FileSourceToolSupport.GetEditPath(services, existing.ItemId);

            return $"Nothing was saved: '{secret}' looks like a password or a key, and tools never take secrets. Save the other settings without it, then a person enters it on the file source's page: {page}";
        }

        if (!TryReadObjects(arguments, out var objects, out var invalid))
        {
            return $"Nothing was saved: {invalid} must be an object.";
        }

        FileSource working;

        if (existing is not null)
        {
            working = existing.Clone();
        }
        else
        {
            var connectorName = AIModuleTools.ReadString(arguments, "connector");
            var connector = FileSourceToolSupport.GetConnectors(services)
                .FirstOrDefault(entry => string.Equals(entry.Name, connectorName, StringComparison.OrdinalIgnoreCase));

            if (connector is null)
            {
                return $"Nothing was saved: name the connector in connector, one of {string.Join(", ", FileSourceToolSupport.GetConnectors(services).Select(entry => entry.Name))}.";
            }

            if (string.Equals(connector.Name, FileSourceToolSupport.SftpConnectorName, StringComparison.OrdinalIgnoreCase))
            {
                return $"Nothing was saved: an SFTP file source needs a password or a private key, which tools never take. Create it on its page: {FileSourceToolSupport.GetCreatePath(services, connector.Name)}";
            }

            working = await manager.NewAsync(connector.Name, cancellationToken: cancellationToken);
        }

        var errors = new List<string>();
        var changes = ReadChanges(services, arguments, working, isNew: existing is null, objects, errors);

        if (errors.Count > 0)
        {
            return "Nothing was saved: " + string.Join(" ", errors);
        }

        changes(working);

        // What a file source reads becomes what the assistant answers from, so a turn cannot point one at its own
        // knowledge.
        if (await AIModuleTools.FeedsRunningAssistantAsync(services, [existing?.AIDataSourceId, working.AIDataSourceId], cancellationToken))
        {
            return AIModuleTools.RunningAssistantRefusal($"the file source '{working.DisplayText}'", existing is null ? FileSourceToolSupport.GetIndexPath(services) : FileSourceToolSupport.GetEditPath(services, existing.ItemId));
        }

        var validation = await manager.ValidateAsync(working, cancellationToken);

        if (!validation.Succeeded)
        {
            return "Nothing was saved: " + string.Join(" ", validation.Errors.Select(error => error.ErrorMessage));
        }

        if (existing is null)
        {
            await manager.CreateAsync(working, cancellationToken);
            existing = working;
        }
        else
        {
            changes(existing);
            await manager.UpdateAsync(existing, cancellationToken: cancellationToken);
        }

        var dataSource = await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(existing.AIDataSourceId, cancellationToken);

        return AIModuleTools.Json(new
        {
            saved = id is null ? "created" : "updated",
            fileSource = await FileSourceToolSupport.DescribeAsync(services, existing, cancellationToken),
            credentials = DescribeCredentials(services, existing),
            synchronization = dataSource is null
                ? null
                : $"A full synchronization of the data source '{dataSource.DisplayText}' was queued. It runs in the background and generates embeddings, which costs embedding tokens; it has not run yet. The files themselves are read on the file source's schedule or with runAIFileSource.",
        });
    }

    private static string DescribeCredentials(IServiceProvider services, FileSource fileSource)
    {
        if (string.Equals(fileSource.Source, FileSourceToolSupport.FtpConnectorName, StringComparison.OrdinalIgnoreCase) &&
            !FileSourceToolSupport.HasSecret(FileSourceToolSupport.ReadConnection(fileSource, FileSourceToolSupport.FtpConnectionKey), "Password"))
        {
            return $"No FTP password is stored. If the server needs one, a person enters it on the file source's page: {FileSourceToolSupport.GetEditPath(services, fileSource.ItemId)}";
        }

        return null;
    }

    private static bool TryReadObjects(AIFunctionArguments arguments, out Dictionary<string, JsonObject> objects, out string invalid)
    {
        objects = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        invalid = null;

        foreach (var name in (string[])["fileSystem", "remote", "ftp", "sftp"])
        {
            if (!AIModuleTools.TryReadObject(arguments, name, out var value))
            {
                invalid = name;

                return false;
            }

            if (value is not null)
            {
                objects[name] = value;
            }
        }

        return true;
    }

    private static Action<FileSource> ReadChanges(IServiceProvider services, AIFunctionArguments arguments, FileSource current, bool isNew, Dictionary<string, JsonObject> objects, List<string> errors)
    {
        var displayText = AIModuleTools.ReadString(arguments, "displayText");
        var dataSourceId = AIModuleTools.ReadString(arguments, "aiDataSourceId");
        var enabled = ReadBool(arguments, "enabled");
        var interval = ReadInt(arguments, "reindexIntervalMinutes", errors);

        // The file source editor's checks.
        if (string.IsNullOrWhiteSpace(displayText ?? current.DisplayText))
        {
            errors.Add("The name is required: give it in displayText.");
        }

        if (string.IsNullOrWhiteSpace(dataSourceId ?? current.AIDataSourceId))
        {
            errors.Add("A target File data source is required: give its ID in aiDataSourceId.");
        }

        if (interval is < 1)
        {
            errors.Add("The re-read interval must be a positive number of minutes.");
        }

        var settings = ReadSettings(arguments, current, isNew, errors);
        var connector = current.Source;
        Action<FileSource> connectorChanges = null;

        if (string.Equals(connector, FileSystemIngestionConnector.ConnectorName, StringComparison.OrdinalIgnoreCase))
        {
            connectorChanges = ReadFileSystem(services, current, isNew, objects.GetValueOrDefault("fileSystem"), errors);
            Reject(objects, errors, "remote", "ftp", "sftp");
        }
        else if (string.Equals(connector, FileSourceToolSupport.FtpConnectorName, StringComparison.OrdinalIgnoreCase))
        {
            var remote = ReadRemote(current, isNew, objects.GetValueOrDefault("remote"), errors);
            var connection = ReadFtp(current, objects.GetValueOrDefault("ftp"), errors);
            connectorChanges = fileSource =>
            {
                remote?.Invoke(fileSource);
                connection?.Invoke(fileSource);
            };
            Reject(objects, errors, "fileSystem", "sftp");
        }
        else if (string.Equals(connector, FileSourceToolSupport.SftpConnectorName, StringComparison.OrdinalIgnoreCase))
        {
            var remote = ReadRemote(current, isNew, objects.GetValueOrDefault("remote"), errors);
            var connection = ReadSftp(current, objects.GetValueOrDefault("sftp"), errors);
            connectorChanges = fileSource =>
            {
                remote?.Invoke(fileSource);
                connection?.Invoke(fileSource);
            };
            Reject(objects, errors, "fileSystem", "ftp");
        }
        else
        {
            // A connector added by another module: its settings are set on its own page.
            Reject(objects, errors, "fileSystem", "remote", "ftp", "sftp");
        }

        return fileSource =>
        {
            if (displayText is not null)
            {
                fileSource.DisplayText = displayText;
            }

            if (dataSourceId is not null)
            {
                fileSource.AIDataSourceId = dataSourceId;
            }

            if (enabled is not null)
            {
                fileSource.Enabled = enabled.Value;
            }

            if (interval is not null)
            {
                fileSource.ReindexIntervalMinutes = interval;
            }

            if (settings is not null)
            {
                fileSource.Put(Copy(settings));
            }

            connectorChanges?.Invoke(fileSource);
        };
    }

    private static FileSourceMetadata ReadSettings(AIFunctionArguments arguments, FileSource current, bool isNew, List<string> errors)
    {
        var names = new[] { "figureMode", "visionDeploymentName", "utilityDeploymentName", "maxFigureDescriptionsPerDocument", "maxItemsPerRun", "language" };

        if (!isNew && !names.Any(arguments.ContainsKey))
        {
            return null;
        }

        var settings = current.TryGet<FileSourceMetadata>(out var stored) ? Copy(stored) : new FileSourceMetadata();
        var figureMode = AIModuleTools.ReadString(arguments, "figureMode");

        if (figureMode is not null)
        {
            if (Enum.TryParse<FigureProcessingMode>(figureMode, ignoreCase: true, out var mode) && Enum.IsDefined(mode))
            {
                settings.FigureMode = mode;
            }
            else
            {
                errors.Add("figureMode must be Off, Auto or All.");
            }
        }

        if (arguments.ContainsKey("visionDeploymentName"))
        {
            settings.VisionDeploymentName = AIModuleTools.ReadString(arguments, "visionDeploymentName");
        }

        if (arguments.ContainsKey("utilityDeploymentName"))
        {
            settings.UtilityDeploymentName = AIModuleTools.ReadString(arguments, "utilityDeploymentName");
        }

        if (arguments.ContainsKey("language"))
        {
            settings.Language = AIModuleTools.ReadString(arguments, "language");
        }

        var maxFigures = ReadInt(arguments, "maxFigureDescriptionsPerDocument", errors);

        if (maxFigures is < 0)
        {
            errors.Add("The figure limit cannot be negative.");
        }
        else if (maxFigures is not null)
        {
            settings.MaxFigureDescriptionsPerDocument = maxFigures.Value;
        }

        var maxItems = ReadInt(arguments, "maxItemsPerRun", errors);

        if (maxItems is < 1)
        {
            errors.Add("The number of items per run must be a positive number.");
        }
        else if (maxItems is not null)
        {
            settings.MaxItemsPerRun = maxItems;
        }

        return settings;
    }

    private static Action<FileSource> ReadFileSystem(IServiceProvider services, FileSource current, bool isNew, JsonObject json, List<string> errors)
    {
        if (!isNew && json is null)
        {
            return null;
        }

        var stored = current.TryGet<FileSystemFileSourceMetadata>(out var folder) ? folder : null;
        var requested = AIModuleTools.TryReadText(json, "rootPath", out var rootPath)
            ? rootPath?.Trim() ?? string.Empty
            : stored?.RootPath ?? string.Empty;
        var recursive = ReadBool(json, "recursive", errors) ?? stored?.Recursive ?? true;
        var maxItems = json is not null && json.ContainsKey("maxItems") ? ReadInt(json, "maxItems", errors) : stored?.MaxItems;

        // The file system editor's checks: the folder is this tenant's file-source folder or inside it.
        string storedPath = null;
        var tenantRoot = services.GetRequiredService<ITenantFileSourceRoot>();

        if (!tenantRoot.TryResolve(requested, out var resolved, out var reason))
        {
            errors.Add($"{reason} Choose this tenant's file-source folder or a folder inside it.");
        }
        else
        {
            storedPath = tenantRoot.ToStoredValue(resolved);
        }

        if (maxItems is < 1)
        {
            errors.Add("The number of files must be a positive number.");
        }

        return fileSource => fileSource.Put(new FileSystemFileSourceMetadata
        {
            RootPath = storedPath,
            Recursive = recursive,
            MaxItems = maxItems,
        });
    }

    private static Action<FileSource> ReadRemote(FileSource current, bool isNew, JsonObject json, List<string> errors)
    {
        if (!isNew && json is null)
        {
            return null;
        }

        var stored = current.TryGet<RemoteFileSourceMetadata>(out var folder) ? folder : null;
        var rootPath = AIModuleTools.TryReadText(json, "rootPath", out var given)
            ? (string.IsNullOrWhiteSpace(given) ? "/" : given.Trim())
            : stored?.RootPath ?? "/";
        var recursive = ReadBool(json, "recursive", errors) ?? stored?.Recursive ?? true;
        var maxItems = json is not null && json.ContainsKey("maxItems") ? ReadInt(json, "maxItems", errors) : stored?.MaxItems;

        if (maxItems is < 1)
        {
            errors.Add("The number of files must be a positive number.");
        }

        return fileSource => fileSource.Put(new RemoteFileSourceMetadata
        {
            RootPath = rootPath,
            Recursive = recursive,
            MaxItems = maxItems,
        });
    }

    private static Action<FileSource> ReadFtp(FileSource current, JsonObject json, List<string> errors)
    {
        var stored = FileSourceToolSupport.ReadConnection(current, FileSourceToolSupport.FtpConnectionKey);

        if (json is null)
        {
            // The FTP editor always asks for a host, so a new FTP file source needs one.
            if (FileSourceToolSupport.GetText(stored, "Host") is null)
            {
                errors.Add("A host is required: give it in ftp.host.");
            }

            return null;
        }

        var connection = (JsonObject)stored?.DeepClone() ?? [];

        if (!ApplyFields(json, connection, FileSourceToolSupport.FtpFields, errors))
        {
            return null;
        }

        // The FTP editor's checks: a host, and a port between 1 and 65535.
        CheckHostAndPort(connection, "Host", "Port", "ftp", errors, hostRequired: true);

        // A stored password goes wherever the host points, so the host cannot move while one is stored.
        if (FileSourceToolSupport.FtpSecretMembers.Any(member => FileSourceToolSupport.HasSecret(stored, member)) &&
            Moved(stored, connection, "Host", "Port"))
        {
            errors.Add("The file source stores an FTP password, which would be sent to the new server. Change the host or the port on the file source's page, where the password is entered again.");
        }

        return fileSource => fileSource.Put(FileSourceToolSupport.FtpConnectionKey, connection.DeepClone());
    }

    private static Action<FileSource> ReadSftp(FileSource current, JsonObject json, List<string> errors)
    {
        var stored = FileSourceToolSupport.ReadConnection(current, FileSourceToolSupport.SftpConnectionKey);

        if (json is null)
        {
            return null;
        }

        var connection = (JsonObject)stored?.DeepClone() ?? [];

        if (!ApplyFields(json, connection, FileSourceToolSupport.SftpFields, errors))
        {
            return null;
        }

        // The SFTP editor's checks: a host, a port and a proxy port between 1 and 65535, a user name, and a password or a
        // private key, which only the page can enter.
        CheckHostAndPort(connection, "Host", "Port", "sftp", errors, hostRequired: true);
        CheckHostAndPort(connection, "ProxyHost", "ProxyPort", "sftp", errors, hostRequired: false);

        if (FileSourceToolSupport.GetText(connection, "Username") is not { Length: > 0 })
        {
            errors.Add("A username is required: give it in sftp.username.");
        }

        if (!FileSourceToolSupport.HasSecret(connection, "Password") && !FileSourceToolSupport.HasSecret(connection, "PrivateKey"))
        {
            errors.Add("The file source has no password or private key. A person provides one on the file source's page.");
        }

        if (FileSourceToolSupport.SftpSecretMembers.Any(member => FileSourceToolSupport.HasSecret(stored, member)) &&
            Moved(stored, connection, "Host", "Port"))
        {
            errors.Add("The file source stores an SFTP password or key, which would be sent to the new server. Change the host or the port on the file source's page, where the credential is entered again.");
        }

        if (FileSourceToolSupport.HasSecret(stored, FileSourceToolSupport.SftpProxySecretMember) &&
            Moved(stored, connection, "ProxyHost", "ProxyPort"))
        {
            errors.Add("The file source stores a proxy password, which would be sent to the new proxy. Change the proxy on the file source's page.");
        }

        return fileSource => fileSource.Put(FileSourceToolSupport.SftpConnectionKey, connection.DeepClone());
    }

    private static bool ApplyFields(JsonObject json, JsonObject connection, IReadOnlyList<(string Argument, string Member)> fields, List<string> errors)
    {
        var count = errors.Count;

        foreach (var (argument, member) in fields)
        {
            if (!json.TryGetPropertyValue(argument, out var node))
            {
                continue;
            }

            if (node is null)
            {
                FileSourceToolSupport.SetMember(connection, member, null);

                continue;
            }

            if (node is JsonValue value && value.GetValueKind() is JsonValueKind.True or JsonValueKind.False)
            {
                FileSourceToolSupport.SetMember(connection, member, JsonValue.Create(value.GetValue<bool>()));
            }
            else if (node is JsonValue number && number.GetValueKind() == JsonValueKind.Number)
            {
                if (!number.TryGetValue<int>(out var whole))
                {
                    errors.Add($"{argument} must be a whole number.");

                    continue;
                }

                FileSourceToolSupport.SetMember(connection, member, JsonValue.Create(whole));
            }
            else if (node is JsonValue text && text.TryGetValue<string>(out var raw))
            {
                var trimmed = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

                FileSourceToolSupport.SetMember(connection, member, trimmed is null ? null : JsonValue.Create(trimmed));
            }
            else
            {
                errors.Add($"{argument} must be a text, a number or true or false.");
            }
        }

        return errors.Count == count;
    }

    private static void CheckHostAndPort(JsonObject connection, string hostMember, string portMember, string prefix, List<string> errors, bool hostRequired)
    {
        if (hostRequired && FileSourceToolSupport.GetText(connection, hostMember) is not { Length: > 0 })
        {
            errors.Add($"A host is required: give it in {prefix}.host.");
        }

        var port = FileSourceToolSupport.GetMember(connection, portMember);

        if (port is JsonValue value && (!value.TryGetValue<int>(out var number) || number is < 1 or > 65535))
        {
            errors.Add(hostMember == "Host"
                ? "The port must be between 1 and 65535."
                : "The proxy port must be between 1 and 65535.");
        }
    }

    private static bool Moved(JsonObject stored, JsonObject next, string hostMember, string portMember)
        => !string.Equals(FileSourceToolSupport.GetText(stored, hostMember), FileSourceToolSupport.GetText(next, hostMember), StringComparison.OrdinalIgnoreCase) ||
            FileSourceToolSupport.GetMember(stored, portMember)?.ToJsonString() != FileSourceToolSupport.GetMember(next, portMember)?.ToJsonString();

    private static void Reject(Dictionary<string, JsonObject> objects, List<string> errors, params string[] names)
    {
        foreach (var name in names)
        {
            if (objects.ContainsKey(name))
            {
                errors.Add($"{name} does not apply to this file source's connector.");
            }
        }
    }

    private static bool? ReadBool(AIFunctionArguments arguments, string name)
        => arguments.TryGetValue(name, out var raw) && raw is not null
            ? arguments.GetFirstValueOrDefault<bool?>(name, null)
            : null;

    private static bool? ReadBool(JsonObject json, string name, List<string> errors)
    {
        if (!AIModuleTools.TryReadBool(json, name, out var value))
        {
            errors.Add($"{name} must be true or false.");
        }

        return value;
    }

    private static int? ReadInt(AIFunctionArguments arguments, string name, List<string> errors)
    {
        if (!arguments.TryGetValue(name, out var raw) || raw is null)
        {
            return null;
        }

        var value = arguments.GetFirstValueOrDefault<int?>(name, null);

        if (value is null)
        {
            errors.Add($"{name} must be a whole number.");
        }

        return value;
    }

    private static int? ReadInt(JsonObject json, string name, List<string> errors)
    {
        if (!AIModuleTools.TryReadInt(json, name, out var value))
        {
            errors.Add($"{name} must be a whole number.");
        }

        return value;
    }

    private static FileSourceMetadata Copy(FileSourceMetadata metadata)
        => new()
        {
            FigureMode = metadata.FigureMode,
            VisionDeploymentName = metadata.VisionDeploymentName,
            UtilityDeploymentName = metadata.UtilityDeploymentName,
            MaxFigureDescriptionsPerDocument = metadata.MaxFigureDescriptionsPerDocument,
            MaxItemsPerRun = metadata.MaxItemsPerRun,
            Language = metadata.Language,
        };
}
