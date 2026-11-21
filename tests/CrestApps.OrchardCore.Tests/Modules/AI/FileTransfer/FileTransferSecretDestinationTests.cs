using CrestApps.Core;
using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Ftp.Models;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.OrchardCore.AI.FileTransfer.Core;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Drivers;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileTransfer;

/// <summary>
/// MD5 (fix verification, round 6): the FTP and SFTP editors of the file sources and the MCP resources kept a stored
/// password or private key when the field came back blank, whatever server the connection now named. A user who may edit
/// the connection, but never saw its password, could point it at a server of their own and receive the password with the
/// next listing, or use a stored private key to sign in to another server or account. A change of host, port or user name,
/// and for SFTP of the host key fingerprint, now drops the stored secrets, as the AI connection editors drop theirs.
/// </summary>
public sealed class FileTransferSecretDestinationTests
{
    private const string Fingerprint = "SHA256:+DiY3wvvV6TuJJhbpZisF/zLDA0zPMSvHdkr4UvCOqU";
    private const string OtherFingerprint = "SHA256:nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Theory]
    [InlineData("attacker.example", 21, "deploy")]
    [InlineData("ftp.vendor.example", 2121, "deploy")]
    [InlineData("ftp.vendor.example", 21, "other")]
    public async Task FtpEditor_DropsThePassword_WhenTheServerOrUserNameChanges(string host, int port, string username)
    {
        // Arrange
        var resource = FtpResource(out _);
        var context = CreateContext(new FtpConnectionFieldsViewModel { Host = host, Port = port, Username = username, EncryptionMode = "Explicit" });

        // Act
        await FtpDriver().UpdateAsync(resource, context);

        // Assert
        Assert.Null(resource.GetOrCreate<FtpConnectionMetadata>().Password);
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(FtpConnectionFieldsViewModel.Password)));
    }

    [Fact]
    public async Task FtpEditor_KeepsThePassword_ForTheSameServerAndUserName()
    {
        // Arrange
        var resource = FtpResource(out var stored);
        var context = CreateContext(new FtpConnectionFieldsViewModel { Host = " FTP.vendor.example ", Port = 21, Username = "deploy", EncryptionMode = "Explicit" });

        // Act
        await FtpDriver().UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(stored, resource.GetOrCreate<FtpConnectionMetadata>().Password);
    }

    [Fact]
    public async Task FtpEditor_StoresANewPassword_GivenWithAnotherServer()
    {
        // Arrange
        var resource = FtpResource(out _);
        var context = CreateContext(new FtpConnectionFieldsViewModel { Host = "ftp.other.example", Port = 21, Username = "deploy", Password = "new", EncryptionMode = "Explicit" });

        // Act
        await FtpDriver().UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("new", _protection.CreateProtector(FtpResourceConstants.DataProtectionPurpose).Unprotect(resource.GetOrCreate<FtpConnectionMetadata>().Password));
    }

    [Theory]
    [InlineData("attacker.example", null, "deploy", Fingerprint)]
    [InlineData("sftp.vendor.example", 2222, "deploy", Fingerprint)]
    [InlineData("sftp.vendor.example", null, "root", Fingerprint)]
    [InlineData("sftp.vendor.example", null, "deploy", OtherFingerprint)]
    public async Task SftpEditor_DropsThePasswordAndKey_WhenTheServerUserNameOrHostKeyChanges(string host, int? port, string username, string fingerprint)
    {
        // Arrange
        var resource = SftpResource();
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = host, Port = port, Username = username, HostKeyFingerprint = fingerprint });

        // Act
        await SftpDriver().UpdateAsync(resource, context);

        // Assert
        var connection = resource.GetOrCreate<SftpConnectionMetadata>();
        Assert.Null(connection.Password);
        Assert.Null(connection.PrivateKey);
        Assert.Null(connection.Passphrase);
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(SftpConnectionFieldsViewModel.Password)));
    }

    [Fact]
    public async Task SftpEditor_KeepsTheSecrets_ForTheSameServerUserNameAndHostKey()
    {
        // Arrange
        var resource = SftpResource();
        var stored = resource.GetOrCreate<SftpConnectionMetadata>();
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "SFTP.vendor.example", Username = "deploy", HostKeyFingerprint = Fingerprint, ProxyType = "Http", ProxyHost = "proxy.vendor.example", ProxyPort = 8080, ProxyUsername = "relay" });

        // Act
        await SftpDriver().UpdateAsync(resource, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        var connection = resource.GetOrCreate<SftpConnectionMetadata>();
        Assert.Equal(stored.Password, connection.Password);
        Assert.Equal(stored.PrivateKey, connection.PrivateKey);
        Assert.Equal(stored.Passphrase, connection.Passphrase);
        Assert.Equal(stored.ProxyPassword, connection.ProxyPassword);
    }

    [Fact]
    public async Task SftpEditor_DropsOnlyTheProxyPassword_WhenTheProxyMoves()
    {
        // Arrange
        var resource = SftpResource();
        var stored = resource.GetOrCreate<SftpConnectionMetadata>();
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "sftp.vendor.example", Username = "deploy", HostKeyFingerprint = Fingerprint, ProxyType = "Http", ProxyHost = "proxy.attacker.example", ProxyPort = 8080, ProxyUsername = "relay" });

        // Act
        await SftpDriver().UpdateAsync(resource, context);

        // Assert
        var connection = resource.GetOrCreate<SftpConnectionMetadata>();
        Assert.Null(connection.ProxyPassword);
        Assert.Equal(stored.Password, connection.Password);
    }

    private McpResource FtpResource(out string stored)
    {
        stored = _protection.CreateProtector(FtpResourceConstants.DataProtectionPurpose).Protect("ftp-secret");
        var resource = new McpResource { Source = FtpResourceConstants.Type };
        resource.Put(new FtpConnectionMetadata { Host = "ftp.vendor.example", Port = 21, Username = "deploy", Password = stored, EncryptionMode = "Explicit" });

        return resource;
    }

    private McpResource SftpResource()
    {
        var protector = _protection.CreateProtector(SftpResourceConstants.DataProtectionPurpose);
        var resource = new McpResource { Source = SftpResourceConstants.Type };
        resource.Put(new SftpConnectionMetadata
        {
            Host = "sftp.vendor.example",
            Username = "deploy",
            HostKeyFingerprint = Fingerprint,
            Password = protector.Protect("sftp-password"),
            PrivateKey = protector.Protect("-----BEGIN OPENSSH PRIVATE KEY-----"),
            Passphrase = protector.Protect("key-passphrase"),
            ProxyType = "Http",
            ProxyHost = "proxy.vendor.example",
            ProxyPort = 8080,
            ProxyUsername = "relay",
            ProxyPassword = protector.Protect("proxy-password"),
        });

        return resource;
    }

    private FtpResourceDisplayDriver FtpDriver()
        => new(_protection, new PassThroughLocalizer<FtpResourceDisplayDriver>(), Options.Create(new FileTransferHostOptions()));

    private SftpResourceDisplayDriver SftpDriver()
        => new(_protection, new PassThroughLocalizer<SftpResourceDisplayDriver>());

    private static UpdateEditorContext CreateContext(object submitted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(submitted));

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
