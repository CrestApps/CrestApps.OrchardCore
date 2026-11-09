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
    private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();

    [Fact]
    public async Task FtpResourceDriver_WhenThePasswordIsLeftBlank_ShouldKeepTheStoredOne()
    {
        // Arrange
        var stored = _dataProtectionProvider.CreateProtector(FtpResourceConstants.DataProtectionPurpose).Protect("secret");
        var resource = new McpResource { Source = FtpResourceConstants.Type };
        resource.Put(new FtpConnectionMetadata { Host = "old.example.com", Password = stored });

        var driver = new FtpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<FtpResourceDisplayDriver>());
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
        var driver = new FtpResourceDisplayDriver(_dataProtectionProvider, new PassThroughLocalizer<FtpResourceDisplayDriver>());
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
        var context = CreateContext(new SftpConnectionFieldsViewModel { Host = "sftp.example.com", Username = "deploy", Password = "  s3cret " });

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

    private static UpdateEditorContext CreateContext(object submitted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(submitted));

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
