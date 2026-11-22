using System.Net;
using System.Net.Sockets;
using CrestApps.Core;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Indexing;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.FileTransfer.Core;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Drivers;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileTransfer;

/// <summary>
/// Review 11 (Mild): FTP and SFTP connections had no egress check, so a tenant's administrator could name a server or
/// an SFTP proxy on the site's own network, to probe internal ports or read internal shares. The editors, the catalog
/// (API and recipe) and every connection now refuse such a server, unless the host allows a private network for file
/// transfers. The editors resolve names over a fixed DNS answer: <c>inside.example.com</c> is on the site's own network.
/// </summary>
public sealed class FileTransferEgressTests
{
    private const string Fingerprint = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Theory]
    [InlineData("10.0.0.5", null, nameof(SftpConnectionFieldsViewModel.Host))]
    [InlineData("localhost", null, nameof(SftpConnectionFieldsViewModel.Host))]
    [InlineData("inside.example.com", null, nameof(SftpConnectionFieldsViewModel.Host))]
    [InlineData("missing.example.com", null, nameof(SftpConnectionFieldsViewModel.Host))]
    [InlineData("sftp.example.com", "169.254.169.254", nameof(SftpConnectionFieldsViewModel.ProxyHost))]
    [InlineData("sftp.example.com", "192.168.1.1", nameof(SftpConnectionFieldsViewModel.ProxyHost))]
    public async Task SftpEditor_RefusesAServerOrProxyOnTheSiteOwnNetwork(string host, string proxyHost, string field)
    {
        var driver = Create<SftpResourceDisplayDriver>(new FileTransferHostOptions());
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        var context = Context(new SftpConnectionFieldsViewModel { Host = host, ProxyHost = proxyHost, ProxyPort = proxyHost is null ? null : 3128, Username = "deploy", Password = "s3cret", HostKeyFingerprint = Fingerprint });

        await driver.UpdateAsync(resource, context);

        Assert.True(context.Updater.ModelState.ContainsKey(field), string.Join(", ", context.Updater.ModelState.Keys));
    }

    [Fact]
    public async Task SftpEditor_AcceptsAPublicServer()
    {
        var driver = Create<SftpResourceDisplayDriver>(new FileTransferHostOptions());
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        var context = Context(new SftpConnectionFieldsViewModel { Host = "sftp.example.com", Username = "deploy", Password = "s3cret", HostKeyFingerprint = Fingerprint });

        await driver.UpdateAsync(resource, context);

        Assert.True(context.Updater.ModelState.IsValid, string.Join(", ", context.Updater.ModelState.Keys));
    }

    [Fact]
    public async Task SftpEditor_AcceptsAPrivateServer_WhereTheHostAllowsIt_ButNeverTheMetadataService()
    {
        var driver = Create<SftpResourceDisplayDriver>(new FileTransferHostOptions { AllowPrivateNetwork = true });

        var allowed = Context(new SftpConnectionFieldsViewModel { Host = "10.0.0.5", Username = "deploy", Password = "s3cret", HostKeyFingerprint = Fingerprint });
        await driver.UpdateAsync(new McpResource { Source = SftpResourceConstants.Type }, allowed);

        var refused = Context(new SftpConnectionFieldsViewModel { Host = "169.254.169.254", Username = "deploy", Password = "s3cret", HostKeyFingerprint = Fingerprint });
        await driver.UpdateAsync(new McpResource { Source = SftpResourceConstants.Type }, refused);

        Assert.True(allowed.Updater.ModelState.IsValid, string.Join(", ", allowed.Updater.ModelState.Keys));
        Assert.True(refused.Updater.ModelState.ContainsKey(nameof(SftpConnectionFieldsViewModel.Host)));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("inside.example.com")]
    public async Task FtpEditor_RefusesAServerOnTheSiteOwnNetwork(string host)
    {
        var driver = Create<FtpResourceDisplayDriver>(new FileTransferHostOptions());
        var context = Context(new FtpConnectionFieldsViewModel { Host = host, Port = 21, EncryptionMode = "Explicit" });

        await driver.UpdateAsync(new McpResource { Source = FtpResourceConstants.Type }, context);

        Assert.True(context.Updater.ModelState.ContainsKey(nameof(FtpConnectionFieldsViewModel.Host)));
    }

    [Theory]
    [InlineData("PassiveAllowUnroutable")]
    [InlineData("PASVUSE")]
    public async Task FtpEditor_RefusesThePassiveModeThatConnectsWhereverTheServerSays(string dataConnectionType)
    {
        // FluentFTP's PASVUSE takes the address in the server's PASV answer as it is, even one on the site's own network.
        var driver = Create<FtpResourceDisplayDriver>(new FileTransferHostOptions());
        var context = Context(new FtpConnectionFieldsViewModel { Host = "ftp.example.com", Port = 21, EncryptionMode = "Explicit", DataConnectionType = dataConnectionType });

        await driver.UpdateAsync(new McpResource { Source = FtpResourceConstants.Type }, context);

        Assert.True(context.Updater.ModelState.ContainsKey(nameof(FtpConnectionFieldsViewModel.DataConnectionType)));
    }

    [Fact]
    public async Task SftpFileSource_SavedThroughTheCatalog_OnTheSiteOwnNetwork_IsRefused()
    {
        // The catalog validates a file source through its connector: the editor, the API and the recipe step.
        await using var services = FileSourceServices(new CrestApps.OrchardCore.AI.FileSources.Sftp.Startup());
        await using var scope = services.CreateAsyncScope();
        var connector = scope.ServiceProvider.GetRequiredKeyedService<IIngestionConnector>(SftpIngestionConnector.ConnectorName);
        var result = new ValidationResultDetails();

        await connector.ValidateAsync(SftpSource("127.0.0.1"), result, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(SftpConnectionMetadata.Host)));
    }

    [Fact]
    public async Task SftpFileSource_IsCheckedBeforeEveryConnection()
    {
        using var server = new CountingServer();
        await using var services = FileSourceServices(new CrestApps.OrchardCore.AI.FileSources.Sftp.Startup());
        await using var scope = services.CreateAsyncScope();
        var connector = scope.ServiceProvider.GetRequiredKeyedService<IIngestionConnector>(SftpIngestionConnector.ConnectorName);

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => connector.DiscoverAsync(SftpSource("127.0.0.1", server.Port), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, server.Accepted);
    }

    [Fact]
    public async Task FtpFileSource_IsCheckedBeforeEveryConnection()
    {
        using var server = new CountingServer();
        await using var services = FileSourceServices(new CrestApps.OrchardCore.AI.FileSources.Ftp.Startup());
        await using var scope = services.CreateAsyncScope();
        var connector = scope.ServiceProvider.GetRequiredKeyedService<IIngestionConnector>(FtpIngestionConnector.ConnectorName);
        var source = new FileSource { ItemId = "fs-ftp", Source = FtpIngestionConnector.ConnectorName, DisplayText = "Share" };
        source.Put(new FtpConnectionMetadata { Host = "127.0.0.1", Port = server.Port, EncryptionMode = "Explicit" });

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => connector.DiscoverAsync(source, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, server.Accepted);
    }

    [Fact]
    public async Task SftpResource_IsCheckedBeforeEveryRead()
    {
        using var server = new CountingServer();
        var services = new ServiceCollection();
        AddCommon(services);
        new CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Startup(new PassThroughStringLocalizer<CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Startup>()).ConfigureServices(services);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredKeyedService<IMcpResourceTypeHandler>(SftpResourceConstants.Type);

        var resource = new McpResource { ItemId = "res-1", Source = SftpResourceConstants.Type, Resource = new ModelContextProtocol.Protocol.Resource { Uri = "sftp://x/{path}", Name = "x" } };
        resource.Put(new SftpConnectionMetadata { Host = "127.0.0.1", Port = server.Port, Username = "deploy", Password = _protection.CreateProtector(SftpResourceConstants.DataProtectionPurpose).Protect("s3cret"), HostKeyFingerprint = Fingerprint });

        var exception = await Assert.ThrowsAnyAsync<Exception>(() => handler.ReadAsync(resource, new Dictionary<string, string> { ["path"] = "a.txt" }, TestContext.Current.CancellationToken));

        Assert.Contains(EgressNetwork.RefusedMessage, exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, server.Accepted);
    }

    [Fact]
    public async Task SftpResource_SavedThroughTheCatalog_OnTheSiteOwnNetwork_IsRefused()
    {
        var services = new ServiceCollection();
        AddCommon(services);
        new CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Startup(new PassThroughStringLocalizer<CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Startup>()).ConfigureServices(services);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var resource = new McpResource { ItemId = "res-2", Source = SftpResourceConstants.Type };
        resource.Put(new SftpConnectionMetadata { Host = "sftp.example.com", ProxyHost = "10.1.2.3", ProxyPort = 1080, Username = "deploy", HostKeyFingerprint = Fingerprint });
        var context = new ValidatingContext<McpResource>(resource);

        foreach (var handler in scope.ServiceProvider.GetServices<ICatalogEntryHandler<McpResource>>())
        {
            await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);
        }

        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(SftpConnectionMetadata.ProxyHost)));
    }

    private TDriver Create<TDriver>(FileTransferHostOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDataProtectionProvider>(_protection);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton(Options.Create(options));
        services.AddSingleton(new FileTransferEgressPolicy(Options.Create(options), hostEnvironment: null, Resolve));

        return ActivatorUtilities.CreateInstance<TDriver>(services.BuildServiceProvider());
    }

    private static IPAddress[] Resolve(string host)
        => host switch
        {
            "inside.example.com" => [IPAddress.Parse("10.0.0.9")],
            "missing.example.com" => throw new SocketException((int)SocketError.HostNotFound),
            _ => [IPAddress.Parse("203.0.113.10")],
        };

    private ServiceProvider FileSourceServices(global::OrchardCore.Modules.StartupBase startup)
    {
        var services = new ServiceCollection();
        AddCommon(services);
        startup.ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private void AddCommon(IServiceCollection services)
    {
        services.AddOptions();
        services.AddLogging();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton<IDataProtectionProvider>(_protection);
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton<global::OrchardCore.Environment.Shell.Configuration.IShellConfiguration>(new global::OrchardCore.Environment.Shell.Configuration.ShellConfiguration(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));
    }

    private FileSource SftpSource(string host, int port = 22)
    {
        var source = new FileSource { ItemId = "fs-sftp", Source = SftpIngestionConnector.ConnectorName, DisplayText = "Share" };
        source.Put(new SftpConnectionMetadata
        {
            Host = host,
            Port = port,
            Username = "deploy",
            Password = _protection.CreateProtector(SftpResourceConstants.DataProtectionPurpose).Protect("s3cret"),
            HostKeyFingerprint = Fingerprint,
        });

        return source;
    }

    private static UpdateEditorContext Context(object submitted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(submitted));

    // Counts the connections made to it on this machine, and closes each.
    private sealed class CountingServer : IDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private int _accepted;

        public CountingServer()
        {
            _listener.Start();
            _ = Task.Run(AcceptAsync, CancellationToken.None);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public int Accepted => Volatile.Read(ref _accepted);

        public void Dispose()
        {
            _stop.Cancel();
            _listener.Stop();
            _stop.Dispose();
        }

        private async Task AcceptAsync()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    Interlocked.Increment(ref _accepted);
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }
            }
        }
    }
}
