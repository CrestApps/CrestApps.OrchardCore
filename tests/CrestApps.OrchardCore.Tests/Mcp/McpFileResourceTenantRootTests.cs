using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Handlers;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using OrchardCore.Environment.Shell;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Mcp;

/// <summary>
/// SEC-A2: an MCP "File" resource used to read from the whole application content root, so a tenant could publish a
/// resource that served <c>App_Data/tenants.json</c>, <c>appsettings.json</c> or another tenant's database. It now reads
/// only from the tenant's own folder, refuses anything outside it, and caps how much it reads.
/// </summary>
public sealed class McpFileResourceTenantRootTests : IDisposable
{
    private readonly string _appData;

    public McpFileResourceTenantRootTests()
    {
        _appData = Path.Combine(Path.GetTempPath(), "mcp-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_appData, "Sites", "TenantA", "mcp-files"));
        Directory.CreateDirectory(Path.Combine(_appData, "Sites", "TenantB", "mcp-files"));

        File.WriteAllText(Path.Combine(_appData, "tenants.json"), "{ \"secret\": true }");
        File.WriteAllText(Path.Combine(_appData, "Sites", "TenantB", "mcp-files", "b.txt"), "tenant B");
        File.WriteAllText(Path.Combine(_appData, "Sites", "TenantA", "mcp-files", "readme.txt"), "hello");
    }

    [Theory]
    [InlineData("../../tenants.json")]
    [InlineData("../../../appsettings.json")]
    [InlineData("../../TenantB/mcp-files/b.txt")]
    [InlineData("App_Data/tenants.json")]
    public async Task ReadAsync_OutsideTheTenantFolder_IsNotFound(string fileName)
    {
        var result = await ReadAsync(fileName);

        Assert.True(IsError(result), $"'{fileName}' should not be readable.");
    }

    [Fact]
    public async Task ReadAsync_WithAnAbsolutePath_IsNotFound()
    {
        var result = await ReadAsync(Path.Combine(_appData, "tenants.json"));

        Assert.True(IsError(result));
    }

    [Fact]
    public async Task ReadAsync_InsideTheTenantFolder_ReadsTheFile()
    {
        var result = await ReadAsync("readme.txt");

        var text = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("hello", text.Text);
    }

    /// <summary>
    /// A link inside the tenant folder that points outside it is not followed: a file link to <c>tenants.json</c>, or,
    /// where this account may not create one (Windows without the privilege), a directory junction to App_Data.
    /// </summary>
    [Fact]
    public async Task ReadAsync_ThroughALinkThatPointsOutsideTheTenantFolder_IsNotFound()
    {
        var folder = Path.Combine(_appData, "Sites", "TenantA", "mcp-files");
        string fileName = null;

        if (TryCreateFileLink(Path.Combine(folder, "linked.txt"), Path.Combine(_appData, "tenants.json")))
        {
            fileName = "linked.txt";
        }
        else if (TryCreateJunction(Path.Combine(folder, "outside"), _appData))
        {
            fileName = "outside/tenants.json";
        }

        if (fileName is null)
        {
            // Skipping is honest; asserting on an unattempted link would not be.
            Assert.SkipWhen(true, "This account cannot create a link.");

            return;
        }

        try
        {
            var result = await ReadAsync(fileName);

            Assert.True(IsError(result), $"'{fileName}' leads out of the tenant folder and should not be readable.");
        }
        finally
        {
            // The junction points at the folder that holds it; removing the link first lets the folder be deleted.
            if (Directory.Exists(Path.Combine(folder, "outside")))
            {
                Directory.Delete(Path.Combine(folder, "outside"));
            }
        }
    }

    [Fact]
    public async Task ReadAsync_AFileOverTheLimit_IsRefused()
    {
        File.WriteAllBytes(Path.Combine(_appData, "Sites", "TenantA", "mcp-files", "big.txt"), new byte[2048]);

        var result = await ReadAsync("big.txt", maxBytes: 1024);

        Assert.True(IsError(result));
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

    private async Task<ReadResourceResult> ReadAsync(string fileName, long maxBytes = 1024 * 1024)
    {
        var shellOptions = Options.Create(new ShellOptions
        {
            ShellsApplicationDataPath = _appData,
            ShellsContainerName = "Sites",
        });

        var resolver = new DefaultMcpFileProviderResolver(new ShellSettings { Name = "TenantA" }, shellOptions);
        var handler = new FileResourceTypeHandler(resolver, Options.Create(new McpFileResourceOptions { MaxBytes = maxBytes }), NullLogger<FileResourceTypeHandler>.Instance);

        var resource = new McpResource
        {
            ItemId = "resource-1",
            Resource = new Resource { Uri = "file://{fileName}", Name = "file" },
        };

        return await handler.ReadAsync(resource, new Dictionary<string, string> { ["fileName"] = fileName }, TestContext.Current.CancellationToken);
    }

    private static bool TryCreateFileLink(string link, string target)
    {
        try
        {
            File.CreateSymbolicLink(link, target);

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateJunction(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        process?.WaitForExit(15_000);

        return Directory.Exists(link);
    }

    private static bool IsError(ReadResourceResult result)
        => result.Contents.Count == 1 &&
            result.Contents[0] is TextResourceContents text &&
            (text.Text.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                text.Text.Contains("too large", StringComparison.OrdinalIgnoreCase));
}
