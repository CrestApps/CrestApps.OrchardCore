using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.FileSources.Connectors;
using CrestApps.Core.AI.FileSources.FileTransfer;
using CrestApps.Core.AI.Ingestion;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.FileSources;
using CrestApps.OrchardCore.AI.FileSources.AI;
using CrestApps.OrchardCore.AI.FileSources.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIKnowledge;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileSources;

/// <summary>
/// The file source tools check the caller's permission, never return or take a password, key or passphrase, apply the
/// file source editors' checks, keep a stored credential bound to its host, and queue a run instead of running it.
/// </summary>
public sealed class FileSourceAIToolTests
{
    private const string Canary = "CANARY-SECRET-7f3a";

    private readonly FakeSourceCatalogManager<FileSource> _fileSources = new();
    private readonly FakeSourceCatalogManager<AIDataSource> _dataSources = new();

    public FileSourceAIToolTests()
    {
        _dataSources.Add(new AIDataSource { ItemId = "file-ds", DisplayText = "Manuals", Source = AIDataSourceSourceTypes.File });
    }

    public static TheoryData<string> ToolNames => new()
    {
        ListAIFileSourcesTool.TheName,
        SaveAIFileSourceTool.TheName,
        DeleteAIFileSourceTool.TheName,
        RunAIFileSourceTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var services = BuildServices(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "fs-1" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageFileSources(string toolName)
    {
        // Arrange
        var services = BuildServices(authorize: (permission, _) => permission.Name != FileSourcePermissions.ManageFileSources.Name);
        AddSftpSource();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new() { ["id"] = "fs-1" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(_fileSources.Deleted);
        Assert.Empty(_fileSources.Updated);
    }

    [Fact]
    public async Task List_NeverReturnsAStoredSecret_OnlyWhetherOneIsStored()
    {
        // Arrange
        var services = BuildServices();
        AddSftpSource();
        AddFtpSource("ftp-1", password: Canary);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIFileSourcesTool(), services);

        // Assert
        Assert.DoesNotContain(Canary, result);

        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var sftp = items.Single(item => item.GetProperty("id").GetString() == "fs-1").GetProperty("sftp");
        Assert.Equal("sftp.example.com", sftp.GetProperty("host").GetString());
        Assert.True(sftp.GetProperty("hasPassword").GetBoolean());
        Assert.True(sftp.GetProperty("hasPrivateKey").GetBoolean());
        Assert.True(sftp.GetProperty("hasPassphrase").GetBoolean());
        Assert.True(sftp.GetProperty("hasProxyPassword").GetBoolean());
        Assert.False(sftp.TryGetProperty("password", out _));

        var ftp = items.Single(item => item.GetProperty("id").GetString() == "ftp-1").GetProperty("ftp");
        Assert.True(ftp.GetProperty("hasPassword").GetBoolean());
        Assert.Contains(json.GetProperty("connectors").EnumerateArray(), connector => connector.GetProperty("name").GetString() == FileSystemIngestionConnector.ConnectorName);
    }

    [Fact]
    public async Task List_SaysSoWhenNoFileSourceHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIFileSourcesTool(), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("No file source has the ID 'missing'", result);
    }

    [Theory]
    [InlineData("ftp", """{ "host": "ftp.example.com", "password": "CANARY-SECRET-7f3a" }""")]
    [InlineData("ftp", """{ "host": "ftp.example.com", "PASSWORD": "CANARY-SECRET-7f3a" }""")]
    [InlineData("sftp", """{ "privateKey": "CANARY-SECRET-7f3a" }""")]
    [InlineData("sftp", """{ "passphrase": "CANARY-SECRET-7f3a" }""")]
    [InlineData("sftp", """{ "proxyPassword": "CANARY-SECRET-7f3a" }""")]
    [InlineData("password", "CANARY-SECRET-7f3a")]
    public async Task Save_RefusesEverySecret(string argument, string value)
    {
        // Arrange
        var services = BuildServices();
        var source = AddSftpSource();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["id"] = source.ItemId,
            [argument] = value.StartsWith('{') ? JsonSerializer.Deserialize<JsonElement>(value) : (object)value,
        });

        // Assert
        Assert.StartsWith("Nothing was saved", result);
        Assert.Contains("tools never take secrets", result);
        Assert.DoesNotContain(Canary, result);
        Assert.Empty(_fileSources.Updated);
    }

    [Fact]
    public async Task Save_CreatesAFileSystemSourceInsideTheTenantFolder()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["connector"] = FileSystemIngestionConnector.ConnectorName,
            ["displayText"] = "Manuals",
            ["aiDataSourceId"] = "file-ds",
            ["reindexIntervalMinutes"] = 720,
            ["fileSystem"] = JsonSerializer.SerializeToElement(new { rootPath = "manuals" }),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Equal("created", json.GetProperty("saved").GetString());
        Assert.Contains("was queued", json.GetProperty("synchronization").GetString());

        var created = Assert.Single(_fileSources.Created);
        Assert.Equal(720, created.ReindexIntervalMinutes);
        Assert.True(created.TryGet<FileSystemFileSourceMetadata>(out var folder));
        Assert.Equal("manuals", folder.RootPath);
        Assert.True(created.TryGet<FileSourceMetadata>(out _));
    }

    [Fact]
    public async Task Save_RefusesAFolderOutsideTheTenantFolder()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["connector"] = FileSystemIngestionConnector.ConnectorName,
            ["displayText"] = "Manuals",
            ["aiDataSourceId"] = "file-ds",
            ["fileSystem"] = JsonSerializer.SerializeToElement(new { rootPath = "../other-tenant" }),
        });

        // Assert
        Assert.Contains("The folder is outside this tenant's folder.", result);
        Assert.Contains("Choose this tenant's file-source folder", result);
        Assert.Empty(_fileSources.Created);
    }

    [Fact]
    public async Task Save_CreatesAnFtpSourceWithoutAPasswordAndLinksToWhereOneIsEntered()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["connector"] = FileSourceToolSupport.FtpConnectorName,
            ["displayText"] = "Policies",
            ["aiDataSourceId"] = "file-ds",
            ["ftp"] = JsonSerializer.SerializeToElement(new { host = "ftp.example.com", port = 21, username = "reader", encryptionMode = "Explicit" }),
            ["remote"] = JsonSerializer.SerializeToElement(new { rootPath = "/policies" }),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        Assert.Contains("No FTP password is stored", json.GetProperty("credentials").GetString());

        var created = Assert.Single(_fileSources.Created);
        var connection = FileSourceToolSupport.ReadConnection(created, FileSourceToolSupport.FtpConnectionKey);
        Assert.Equal("ftp.example.com", FileSourceToolSupport.GetText(connection, "Host"));
        Assert.Equal("Explicit", FileSourceToolSupport.GetText(connection, "EncryptionMode"));
        Assert.False(FileSourceToolSupport.HasSecret(connection, "Password"));
        Assert.True(created.TryGet<RemoteFileSourceMetadata>(out var remote));
        Assert.Equal("/policies", remote.RootPath);
    }

    [Theory]
    [InlineData("other.example.com", 21)]
    [InlineData("ftp.example.com", 2121)]
    public async Task Save_KeepsAStoredFtpPasswordOnItsHost(string host, int port)
    {
        // Arrange
        var services = BuildServices();
        var source = AddFtpSource("ftp-1", password: Canary);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["id"] = source.ItemId,
            ["ftp"] = JsonSerializer.SerializeToElement(new { host, port }),
        });

        // Assert
        Assert.Contains("would be sent to the new server", result);
        Assert.Empty(_fileSources.Updated);
        Assert.Equal("ftp.example.com", FileSourceToolSupport.GetText(FileSourceToolSupport.ReadConnection(source, FileSourceToolSupport.FtpConnectionKey), "Host"));
    }

    [Fact]
    public async Task Save_UpdatesAnFtpSourceAndKeepsItsStoredPassword()
    {
        // Arrange
        var services = BuildServices();
        var source = AddFtpSource("ftp-1", password: Canary);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["id"] = source.ItemId,
            ["ftp"] = JsonSerializer.SerializeToElement(new { username = "other-reader" }),
        });

        // Assert
        Assert.DoesNotContain(Canary, result);
        Assert.Same(source, Assert.Single(_fileSources.Updated));

        var connection = FileSourceToolSupport.ReadConnection(source, FileSourceToolSupport.FtpConnectionKey);
        Assert.Equal("other-reader", FileSourceToolSupport.GetText(connection, "Username"));
        Assert.Equal(Canary, FileSourceToolSupport.GetText(connection, "Password"));
    }

    [Fact]
    public async Task Save_RefusesAPortTheEditorRefuses()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["connector"] = FileSourceToolSupport.FtpConnectorName,
            ["displayText"] = "Policies",
            ["aiDataSourceId"] = "file-ds",
            ["ftp"] = JsonSerializer.SerializeToElement(new { host = "ftp.example.com", port = 70000 }),
        });

        // Assert
        Assert.Contains("The port must be between 1 and 65535.", result);
        Assert.Empty(_fileSources.Created);
    }

    [Fact]
    public async Task Save_DoesNotCreateAnSftpSource_ItNeedsACredential()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveAIFileSourceTool(), services, new()
        {
            ["connector"] = FileSourceToolSupport.SftpConnectorName,
            ["displayText"] = "Reports",
            ["aiDataSourceId"] = "file-ds",
        });

        // Assert
        Assert.Contains("needs a password or a private key", result);
        Assert.Empty(_fileSources.Created);
    }

    [Fact]
    public async Task Delete_DeletesTheFileSourceAndSaysTheCleanupWasQueued()
    {
        // Arrange
        var services = BuildServices();
        var source = AddSftpSource();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteAIFileSourceTool(), services, new() { ["id"] = source.ItemId }));

        // Assert
        Assert.Equal(source.ItemId, json.GetProperty("deleted").GetProperty("id").GetString());
        Assert.Contains("cleanup has been queued", json.GetProperty("cleanup").GetString());
        Assert.Same(source, Assert.Single(_fileSources.Deleted));
    }

    [Fact]
    public async Task Delete_SaysSoWhenNoFileSourceHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteAIFileSourceTool(), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no file source has the ID 'missing'", result);
    }

    [Fact]
    public async Task Run_DoesNotClaimARunWasQueuedOutsideAShellScope()
    {
        // Arrange
        var services = BuildServices();
        var source = AddSftpSource();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunAIFileSourceTool(), services, new() { ["id"] = source.ItemId });

        // Assert
        Assert.Contains("could not be queued here", result);
    }

    [Fact]
    public async Task Run_SaysSoWhenNoFileSourceHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RunAIFileSourceTool(), services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no file source has the ID 'missing'", result);
    }

    private FileSource AddSftpSource()
    {
        var source = new FileSource
        {
            ItemId = "fs-1",
            DisplayText = "Reports",
            Source = FileSourceToolSupport.SftpConnectorName,
            AIDataSourceId = "file-ds",
        };

        source.Put(FileSourceToolSupport.SftpConnectionKey, new JsonObject
        {
            ["Host"] = "sftp.example.com",
            ["Port"] = 22,
            ["Username"] = "reader",
            ["Password"] = Canary,
            ["PrivateKey"] = Canary,
            ["Passphrase"] = Canary,
            ["ProxyHost"] = "proxy.example.com",
            ["ProxyPassword"] = Canary,
        });

        return _fileSources.Add(source);
    }

    private FileSource AddFtpSource(string id, string password)
    {
        var source = new FileSource
        {
            ItemId = id,
            DisplayText = "Policies",
            Source = FileSourceToolSupport.FtpConnectorName,
            AIDataSourceId = "file-ds",
        };

        source.Put(FileSourceToolSupport.FtpConnectionKey, new JsonObject
        {
            ["Host"] = "ftp.example.com",
            ["Port"] = 21,
            ["Username"] = "reader",
            ["Password"] = password,
        });

        return _fileSources.Add(source);
    }

    private ServiceProvider BuildServices(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton<ISourceCatalogManager<FileSource>>(_fileSources);
        services.AddSingleton<ISourceCatalogManager<AIDataSource>>(_dataSources);
        services.AddSingleton<ITenantFileSourceRoot>(new FakeTenantRoot());
        services.Configure<IngestionConnectorOptions>(options =>
        {
            options.AddOrUpdate(FileSystemIngestionConnector.ConnectorName, new LocalizedString("Folder", "Folder"), new LocalizedString("Folder", "This tenant's folder."));
            options.AddOrUpdate(FileSourceToolSupport.FtpConnectorName, new LocalizedString("FTP", "FTP"), new LocalizedString("FTP", "An FTP server."));
            options.AddOrUpdate(FileSourceToolSupport.SftpConnectorName, new LocalizedString("SFTP", "SFTP"), new LocalizedString("SFTP", "An SFTP server."));
        });

        return services.BuildServiceProvider();
    }

    private sealed class FakeTenantRoot : ITenantFileSourceRoot
    {
        private const string Root = "/tenants/default/file-sources/";

        public string GetRoot()
            => Root;

        public string EnsureRoot()
            => Root;

        public bool TryResolve(string path, out string resolved, out string reason)
        {
            if (path.Contains("..", StringComparison.Ordinal))
            {
                resolved = null;
                reason = "The folder is outside this tenant's folder.";

                return false;
            }

            resolved = Root + path;
            reason = null;

            return true;
        }

        public string ToStoredValue(string resolvedPath)
            => resolvedPath[Root.Length..];
    }

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListAIFileSourcesTool.TheName => new ListAIFileSourcesTool(),
            SaveAIFileSourceTool.TheName => new SaveAIFileSourceTool(),
            DeleteAIFileSourceTool.TheName => new DeleteAIFileSourceTool(),
            _ => new RunAIFileSourceTool(),
        };
}
