using CrestApps.Core.AI.Ftp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileTransfer;

/// <summary>
/// MD7 (fix verification): CrestApps.Core's FTP client checks each connection against its <see cref="FtpOptions"/>, and
/// nothing in this repository bound them, so a connection that accepts any certificate was refused whatever the host's
/// <c>CrestApps_AI:FileTransfer:AllowAnyFtpCertificate</c> said, the setting the editor reads. Both FTP features now bind
/// the package's options from the host's file transfer settings.
/// </summary>
public sealed class FileTransferHostOptionsTests
{
    [Fact]
    public void FtpFileSources_BindThePackagesOptions_FromTheHostsFileTransferSettings()
    {
        var services = Configure(
            new global::CrestApps.OrchardCore.AI.FileSources.Ftp.Startup(),
            new Dictionary<string, string>
            {
                ["CrestApps_AI:FileTransfer:AllowAnyFtpCertificate"] = "true",
                ["CrestApps_AI:FileTransfer:AllowUnencryptedFtp"] = "true",
            });

        var options = services.GetRequiredService<IOptions<FtpOptions>>().Value;

        Assert.True(options.AllowAnyCertificate);
        Assert.True(options.AllowUnencrypted);
    }

    [Fact]
    public void FtpMcpResources_BindThePackagesOptions_FromTheHostsFileTransferSettings()
    {
        var services = Configure(
            new global::CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Startup(Mock.Of<IStringLocalizer<global::CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Startup>>()),
            new Dictionary<string, string> { ["CrestApps_AI:FileTransfer:AllowAnyFtpCertificate"] = "true" });

        var options = services.GetRequiredService<IOptions<FtpOptions>>().Value;

        Assert.True(options.AllowAnyCertificate);
        Assert.False(options.AllowUnencrypted);
    }

    [Fact]
    public void FtpFileSources_WithoutHostSettings_KeepThePackagesSafeDefaults()
    {
        var services = Configure(new global::CrestApps.OrchardCore.AI.FileSources.Ftp.Startup(), []);

        var options = services.GetRequiredService<IOptions<FtpOptions>>().Value;

        Assert.False(options.AllowAnyCertificate);
        Assert.False(options.AllowUnencrypted);
    }

    private static ServiceProvider Configure(global::OrchardCore.Modules.StartupBase startup, Dictionary<string, string> values)
    {
        var services = new ServiceCollection()
            .AddSingleton<IShellConfiguration>(new ShellConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

        startup.ConfigureServices(services);

        return services.BuildServiceProvider();
    }
}
