using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Configuration;

namespace CrestApps.OrchardCore.Tests.Mcp;

public sealed class McpFileProviderResolverTests : IDisposable
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), "mcp-files-" + Guid.NewGuid().ToString("N"));

    public McpFileProviderResolverTests()
    {
        // Files a File resource must never serve: the tenant's settings and another tenant's data.
        Directory.CreateDirectory(Path.Combine(_appData, "Sites", "Default"));
        Directory.CreateDirectory(Path.Combine(_appData, "Sites", "Other"));
        File.WriteAllText(Path.Combine(_appData, "Sites", "Default", "appsettings.json"), "{ \"secret\": true }");
        File.WriteAllText(Path.Combine(_appData, "Sites", "Other", "data.txt"), "other tenant");
    }

    [Fact]
    public void Resolve_WithoutProviderName_AndNoMcpFilesFolder_FindsNothing()
    {
        var provider = CreateResolver().Resolve(null);

        Assert.NotNull(provider);
        Assert.False(provider.GetFileInfo("appsettings.json").Exists);
    }

    [Fact]
    public void Resolve_WithoutProviderName_ReadsOnlyInsideTheTenantMcpFilesFolder()
    {
        var folder = Path.Combine(_appData, "Sites", "Default", DefaultMcpFileProviderResolver.TenantFolderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "handbook.md"), "# Handbook");
        File.WriteAllText(Path.Combine(folder, ".env"), "KEY=value");

        var provider = CreateResolver().Resolve(string.Empty);

        Assert.True(provider.GetFileInfo("handbook.md").Exists);

        // The client picks the file name, so nothing outside the folder, and no dot file, may be reached.
        Assert.False(provider.GetFileInfo("../appsettings.json").Exists);
        Assert.False(provider.GetFileInfo("../../Other/data.txt").Exists);
        Assert.False(provider.GetFileInfo(Path.Combine(_appData, "Sites", "Default", "appsettings.json")).Exists);
        Assert.False(provider.GetFileInfo(".env").Exists);
    }

    [Theory]
    [InlineData(" .env")]
    [InlineData("/ .env")]
    [InlineData(".env ")]
    [InlineData("docs/ .env")]
    public void Resolve_WithoutProviderName_HidesADotFileBehindSpaces(string path)
    {
        // Arrange
        // Review 3: the tenant folder trims the path, so " .env" (a URI's "%20.env") read the hidden ".env".
        var folder = Path.Combine(_appData, "Sites", "Default", DefaultMcpFileProviderResolver.TenantFolderName);
        Directory.CreateDirectory(Path.Combine(folder, "docs"));
        File.WriteAllText(Path.Combine(folder, ".env"), "KEY=value");
        File.WriteAllText(Path.Combine(folder, "docs", ".env"), "KEY=value");
        var provider = CreateResolver().Resolve(string.Empty);

        // Act
        var file = provider.GetFileInfo(path);

        // Assert
        Assert.False(file.Exists);
    }

    [Fact]
    public void Resolve_WithUnknownProviderName_ReturnsNull()
    {
        Assert.Null(CreateResolver().Resolve("contentroot"));
    }

    [Fact]
    public void Resolve_WithRegisteredProviderName_UsesTheRegisteredProvider()
    {
        var registered = new NullFileProvider();
        var options = new McpFileProviderOptions().AddProvider("handbook", _ => registered);

        Assert.Same(registered, CreateResolver(options).Resolve(" Handbook "));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_appData, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private DefaultMcpFileProviderResolver CreateResolver(McpFileProviderOptions options = null)
    {
        return new DefaultMcpFileProviderResolver(
            new ShellSettings { Name = "Default" },
            Options.Create(new ShellOptions { ShellsApplicationDataPath = _appData, ShellsContainerName = "Sites" }),
            Options.Create(options ?? new McpFileProviderOptions()),
            new EmptyServiceProvider());
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType) => null;
    }
}
