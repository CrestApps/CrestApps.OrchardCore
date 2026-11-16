using CrestApps.Core;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Drivers;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileTransfer;

/// <summary>
/// The FTP and SFTP connection editors of the AI file sources and the MCP resources share one base each, so both
/// validate a connection the same way and keep a stored secret when the field comes back blank.
/// </summary>
public sealed class FileTransferConnectionEditorTests
{
    // A SHA-256 host key fingerprint as ssh-keygen -lf prints it.
    private const string Fingerprint = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";

    private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();

    [Fact]
    public async Task FtpResourceDriver_WhenThePasswordIsLeftBlank_ShouldKeepTheStoredOne()
    {
        // Arrange
        var stored = _dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose).Protect("secret");
        var resource = new McpResource { Source = FtpResourceConstants.Type };
        resource.Put(new FtpConnectionMetadata { Host = "ftp.example.com", Port = 21, Password = stored });

        var driver = new FtpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<FtpResourceDisplayDriver>(), Microsoft.Extensions.Options.Options.Create(new CrestApps.OrchardCore.AI.FileTransfer.Core.FileTransferHostOptions()));
        var context = CreateContext(new FtpConnectionFieldsViewModel { Host = " ftp.example.com ", Port = 21 });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        var connection = resource.GetOrCreate<FtpConnectionMetadata>();
        Assert.Equal("ftp.example.com", connection.Host);
        Assert.Equal(stored, connection.Password);
    }

    [Fact]
    public async Task FtpResourceDriver_WhenThePortIsOutOfRange_ShouldReportIt()
    {
        // Arrange
        var resource = new McpResource { Source = FtpResourceConstants.Type };
        var driver = new FtpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<FtpResourceDisplayDriver>(), Microsoft.Extensions.Options.Options.Create(new CrestApps.OrchardCore.AI.FileTransfer.Core.FileTransferHostOptions()));
        var context = CreateContext(new FtpConnectionFieldsViewModel { Host = "ftp.example.com", Port = 70000 });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(FtpConnectionFieldsViewModel.Port)));
    }

    [Fact]
    public async Task SftpResourceDriver_WhenANewPasswordIsPosted_ShouldProtectIt()
    {
        // Arrange
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        var driver = new SftpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<SftpResourceDisplayDriver>());
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "sftp.example.com", Username = "deploy", Password = "  s3cret ", HostKeyFingerprint = Fingerprint });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        var connection = resource.GetOrCreate<SftpConnectionMetadata>();
        Assert.Equal("s3cret", _dataProtectionProvider.CreateProtector(SftpResourceConstants.DataProtectionPurpose).Unprotect(connection.Password));
    }

    [Fact]
    public async Task SftpResourceDriver_WhenNoPasswordOrKeyIsStored_ShouldAskForOne()
    {
        // Arrange
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        var driver = new SftpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<SftpResourceDisplayDriver>());
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "sftp.example.com", Username = "deploy" });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(SftpConnectionFieldsViewModel.Password)));
    }

    /// <summary>
    /// MD7 (fix verification): since CrestApps.Core preview.375 the SFTP client refuses a connection that names no host
    /// key fingerprint, and the editors had no field for one, so every SFTP file source and MCP resource was refused.
    /// </summary>
    [Fact]
    public async Task SftpResourceDriver_WithoutAHostKeyFingerprint_ShouldAskForOne()
    {
        // Arrange
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        var driver = new SftpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<SftpResourceDisplayDriver>());
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "sftp.example.com", Username = "deploy", Password = "s3cret" });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.ContainsKey("HostKeyFingerprint"));
    }

    [Theory]
    [InlineData("SHA256:not-a-fingerprint")]
    [InlineData("MD5:16:27:ac:a5:76:28:2d:36:63:1b:56:4d:eb:df:a6:48")]
    public async Task SftpResourceDriver_WithAnInvalidHostKeyFingerprint_ShouldReportIt(string fingerprint)
    {
        // Arrange
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        var driver = new SftpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<SftpResourceDisplayDriver>());
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "sftp.example.com", Username = "deploy", Password = "s3cret", HostKeyFingerprint = fingerprint });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(SftpConnectionFieldsViewModel.HostKeyFingerprint)));
    }

    [Theory]
    [InlineData("SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU")]
    [InlineData(" +DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU= ")]
    public async Task SftpResourceDriver_StoresTheFingerprint_SoThePackagesClientTrustsThatServer(string posted)
    {
        // Arrange
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        var driver = new SftpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<SftpResourceDisplayDriver>());
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "sftp.example.com", Username = "deploy", Password = "s3cret", HostKeyFingerprint = posted });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert: CrestApps.Core's own check trusts the server that shows this key, and no other.
        Assert.True(context.Updater.ModelState.IsValid);
        var stored = resource.GetOrCreate<SftpConnectionMetadata>().HostKeyFingerprint;
        Assert.Equal(Fingerprint, stored);
        Assert.True(SftpHostKeys.Matches(stored, "+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU", md5Fingerprint: null));
        Assert.False(SftpHostKeys.Matches(stored, "nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8", md5Fingerprint: null));
    }

    /// <summary>
    /// MD7 (fix verification): the FTP client refuses a connection that sends a user name without TLS unless the host
    /// allows it, so the editor refuses it too instead of saving a connection that can never connect.
    /// </summary>
    [Theory]
    [InlineData("None", false, true)]
    [InlineData("Auto", false, true)]
    [InlineData("Explicit", false, false)]
    [InlineData("None", true, false)]
    public async Task FtpResourceDriver_AUserNameWithoutTls_IsRefusedUnlessTheHostAllowsIt(string encryptionMode, bool allowUnencrypted, bool refused)
    {
        // Arrange
        var resource = new McpResource { Source = FtpResourceConstants.Type };
        var driver = new FtpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<FtpResourceDisplayDriver>(), Microsoft.Extensions.Options.Options.Create(new CrestApps.OrchardCore.AI.FileTransfer.Core.FileTransferHostOptions { AllowUnencryptedFtp = allowUnencrypted }));
        var context = CreateContext(new FtpConnectionFieldsViewModel { Host = "ftp.example.com", Port = 21, Username = "deploy", EncryptionMode = encryptionMode });

        // Act
        await driver.UpdateAsync(resource, context);

        // Assert
        Assert.Equal(refused, context.Updater.ModelState.ContainsKey(nameof(FtpConnectionFieldsViewModel.EncryptionMode)));
    }

    private static UpdateEditorContext CreateContext(object submitted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(submitted));

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
