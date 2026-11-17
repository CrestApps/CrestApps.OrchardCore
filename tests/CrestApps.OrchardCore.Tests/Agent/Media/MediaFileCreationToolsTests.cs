using System.Net;
using CrestApps.OrchardCore.AI.Agent.Media;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.FileStorage;
using OrchardCore.Infrastructure;
using OrchardCore.Media;

namespace CrestApps.OrchardCore.Tests.Agent.Media;

/// <summary>
/// The two tools that put a new file in the media library: one from text, one from a public address. Both follow the
/// upload of the Media Library (folder permission, extension rule, size limit, file creation pipeline, no overwrite
/// unless asked), and the import never fetches an address on the site's own network.
/// </summary>
public sealed class MediaFileCreationToolsTests
{
    [Fact]
    public async Task SaveMediaTextFile_StoresTheTextAndReturnsThePathAndTheUrl()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFolder("exports");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), services, new()
        {
            ["path"] = "exports/prices.csv",
            ["text"] = "sku,price\nA1,9.50\n",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var file = json.GetProperty("file");

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.False(json.GetProperty("replaced").GetBoolean());
        Assert.Equal("exports/prices.csv", file.GetProperty("path").GetString());
        Assert.Equal("/media/exports/prices.csv", file.GetProperty("url").GetString());
        Assert.Equal("text/csv", file.GetProperty("mimeType").GetString());
        Assert.Equal(new[] { "create:exports/prices.csv" }, harness.Store.Changes);
        Assert.Equal("sku,price\nA1,9.50\n", harness.Store.ReadText("exports/prices.csv"));
    }

    [Fact]
    public async Task SaveMediaTextFile_ReplacesAnExistingFileOnlyWhenOverwriteIsSet()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFile("exports/prices.csv", "old");

        using var services = harness.Build();

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), services, new()
        {
            ["path"] = "exports/prices.csv",
            ["text"] = "new",
        });

        var changesAfterRefusal = harness.Store.Changes.Count;

        var replaced = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), services, new()
        {
            ["path"] = "exports/prices.csv",
            ["text"] = "new",
            ["overwrite"] = true,
        });

        // Assert
        Assert.StartsWith("Nothing was saved: the media file 'exports/prices.csv' already exists.", refused, StringComparison.Ordinal);
        Assert.Equal(0, changesAfterRefusal);
        Assert.True(AgentToolTestContext.ReadJson(replaced).GetProperty("replaced").GetBoolean());
        Assert.Equal(new[] { "replace:exports/prices.csv" }, harness.Store.Changes);
        Assert.Equal("new", harness.Store.ReadText("exports/prices.csv"));
    }

    [Fact]
    public async Task SaveMediaTextFile_WithARestrictedExtension_NeedsThePermissionToUploadRestrictedMedia()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFolder("theme");
        harness.Refuse(MediaPermissions.UploadRestrictedMedia);

        using var refusedServices = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["path"] = "theme/site.css",
            ["text"] = "body { margin: 0; }",
        };

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), refusedServices, arguments);

        harness.Authorize = (_, _) => true;

        using var allowedServices = harness.Build();

        var allowed = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), allowedServices, arguments);

        // Assert
        Assert.Contains("the extension '.css' is restricted", refused, StringComparison.Ordinal);
        Assert.Contains(refusedServices.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == "UploadRestrictedMedia");
        Assert.True(AgentToolTestContext.ReadJson(allowed).GetProperty("saved").GetBoolean());
        Assert.Equal(new[] { "create:theme/site.css" }, harness.Store.Changes);
    }

    [Theory]
    [InlineData("exports/logo.png", "x", "this tool writes text files only")]
    [InlineData("exports/notes.md", "x", "the extension '.md' is not allowed on this site")]
    [InlineData("nowhere/prices.csv", "x", "there is no media folder 'nowhere'")]
    [InlineData("exports", "x", "this tool writes text files only")]
    [InlineData("../prices.csv", "x", "is not a media path")]
    public async Task SaveMediaTextFile_WhenTheFileCannotBeStored_StoresNothing(string path, string text, string expected)
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFolder("exports");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), services, new()
        {
            ["path"] = path,
            ["text"] = text,
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task SaveMediaTextFile_WithMoreTextThanTheUploadLimit_StoresNothing()
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), services, new()
        {
            ["path"] = "big.csv",
            ["text"] = new string('a', 1001),
        });

        // Assert
        Assert.Contains("more than the site's upload limit of 1000 bytes", result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task SaveMediaTextFile_WhenAFileEventHandlerRefusesTheFile_StoresNothingAndRelaysTheReason()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.FileEventHandlers.Add(new RefusingFileEventHandler("The file contains a virus."));

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), services, new()
        {
            ["path"] = "prices.csv",
            ["text"] = "sku,price",
        });

        // Assert
        Assert.Equal("Nothing was saved: the media library refused the file. The file contains a virus.", result);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task SaveMediaTextFile_ChecksTheFolderPermissionOnTheFolderOfTheFile()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFolder("_Users/other");
        harness.RefuseFolder("_Users");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveMediaTextFileTool(), services, new()
        {
            ["path"] = "_Users/other/notes.txt",
            ["text"] = "x",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Contains(("ManageMediaFolder", (object)"_Users/other"), services.GetRequiredService<FakeAuthorizationService>().Checks);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task ImportMediaFromUrl_StoresTheFileUnderTheNameOfTheAddress()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFolder("brand");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://example.com/press/Company%20Logo.png?size=large&token=abc",
            ["folder"] = "brand",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var file = json.GetProperty("file");

        Assert.True(json.GetProperty("imported").GetBoolean());
        Assert.False(json.GetProperty("replaced").GetBoolean());
        Assert.Equal("brand/Company Logo.png", file.GetProperty("path").GetString());
        Assert.Equal(9, file.GetProperty("size").GetInt64());
        Assert.Equal("https://example.com/press/Company%20Logo.png", json.GetProperty("source").GetString());
        Assert.DoesNotContain("token", result, StringComparison.Ordinal);
        Assert.Equal(new[] { "create:brand/Company Logo.png" }, harness.Store.Changes);
        Assert.Equal("png-bytes", harness.Store.ReadText("brand/Company Logo.png"));
        Assert.Equal("https://example.com/press/Company%20Logo.png?size=large&token=abc", Assert.Single(harness.Requests).AbsoluteUri);
        Assert.All(harness.TempFiles, path => Assert.False(File.Exists(path)));
    }

    [Fact]
    public async Task ImportMediaFromUrl_WithAFileName_StoresTheFileUnderThatNameInTheRootFolder()
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://example.com/download?id=42",
            ["fileName"] = "some/folder/report.png",
        });

        // Assert
        Assert.Equal("report.png", AgentToolTestContext.ReadJson(result).GetProperty("file").GetProperty("path").GetString());
        Assert.Equal(new[] { "create:report.png" }, harness.Store.Changes);
    }

    [Fact]
    public async Task ImportMediaFromUrl_ReplacesAnExistingFileOnlyWhenOverwriteIsSet()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFile("brand/logo.png", "old");

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["url"] = "https://example.com/logo.png",
            ["folder"] = "brand",
        };

        // Act
        var refused = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, arguments);
        var requestsAfterRefusal = harness.Requests.Count;

        arguments["overwrite"] = true;

        var replaced = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was imported: the media file 'brand/logo.png' already exists.", refused, StringComparison.Ordinal);
        Assert.Equal(0, requestsAfterRefusal);
        Assert.True(AgentToolTestContext.ReadJson(replaced).GetProperty("replaced").GetBoolean());
        Assert.Equal(new[] { "replace:brand/logo.png" }, harness.Store.Changes);
        Assert.Equal("png-bytes", harness.Store.ReadText("brand/logo.png"));
    }

    [Theory]
    [InlineData("https://example.com/tool.exe", null, null, "the extension '.exe' is not allowed on this site")]
    [InlineData("https://example.com/logo.png", "nowhere", null, "there is no media folder 'nowhere'")]
    [InlineData("https://example.com/download?id=42", null, null, "Give fileName")]
    [InlineData("https://example.com/logo.png", "../outside", null, "is not a media path")]
    [InlineData("https://example.com/logo.png", null, "logo", "Give fileName")]
    [InlineData("ftp://example.com/logo.png", null, null, "Only http and https addresses can be imported.")]
    [InlineData("logo.png", null, null, "The url must be a complete address")]
    public async Task ImportMediaFromUrl_WhenTheArgumentsCannotBeUsed_SendsNoRequestAndStoresNothing(string url, string folder, string fileName, string expected)
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["url"] = url,
        };

        if (folder is not null)
        {
            arguments["folder"] = folder;
        }

        if (fileName is not null)
        {
            arguments["fileName"] = fileName;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was imported:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Requests);
        Assert.Empty(harness.Store.Changes);
    }

    [Theory]
    [InlineData("http://localhost/logo.png")]
    [InlineData("http://127.0.0.1:8080/logo.png")]
    [InlineData("http://10.1.2.3/logo.png")]
    [InlineData("http://192.168.1.10/logo.png")]
    [InlineData("http://169.254.169.254/latest/meta-data/logo.png")]
    [InlineData("http://[::1]/logo.png")]
    [InlineData("https://user:secret@example.com/logo.png")]
    public async Task ImportMediaFromUrl_ForAnAddressOnTheSitesOwnNetworkOrWithCredentials_SendsNoRequest(string url)
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = url,
        });

        // Assert
        Assert.StartsWith("Nothing was imported:", result, StringComparison.Ordinal);
        Assert.Empty(harness.Requests);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task ImportMediaFromUrl_WhenTheHostResolvesToAnInternalAddress_SendsNoRequest()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            ResolveHost = _ => [MediaToolHarness.PublicAddress, IPAddress.Parse("10.0.0.5")],
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://files.example.com/logo.png",
        });

        // Assert
        Assert.Contains("on the site's own network", result, StringComparison.Ordinal);
        Assert.Empty(harness.Requests);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task ImportMediaFromUrl_WhenTheFileIsLargerThanTheUploadLimit_StoresNothing()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => MediaToolHarness.CreateResponse(new string('a', 1001), "image/png"),
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://example.com/huge.png",
        });

        // Assert
        Assert.Contains("larger than the site's upload limit of 1000 bytes", result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task ImportMediaFromUrl_WhenTheAddressReturnsAWebPageInPlaceOfTheFile_StoresNothing()
    {
        // Arrange
        var harness = new MediaToolHarness
        {
            Respond = _ => MediaToolHarness.CreateResponse("<html>Sign in</html>", "text/html"),
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://example.com/private/logo.png",
        });

        // Assert
        Assert.Contains("returned a web page, not a '.png' file", result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
        Assert.All(harness.TempFiles, path => Assert.False(File.Exists(path)));
    }

    [Fact]
    public async Task ImportMediaFromUrl_WithARestrictedExtension_NeedsThePermissionToUploadRestrictedMedia()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Refuse(MediaPermissions.UploadRestrictedMedia);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://example.com/icon.svg",
        });

        // Assert
        Assert.Contains("the extension '.svg' is restricted", result, StringComparison.Ordinal);
        Assert.Empty(harness.Requests);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task ImportMediaFromUrl_ChecksTheFolderPermissionOnTheTargetFolder()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFolder("mediafields/BlogPost");
        harness.RefuseFolder("mediafields");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://example.com/logo.png",
            ["folder"] = "mediafields/BlogPost",
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Contains(("ManageMediaFolder", (object)"mediafields/BlogPost"), services.GetRequiredService<FakeAuthorizationService>().Checks);
        Assert.Empty(harness.Requests);
    }

    [Fact]
    public async Task ImportMediaFromUrl_NormalizesTheFileNameAsAnUploadDoes()
    {
        // Arrange
        var normalizer = new Mock<IMediaNameNormalizerService>();

        normalizer
            .Setup(service => service.NormalizeFileName(It.IsAny<string>()))
            .Returns((string name) => name.ToLowerInvariant().Replace(' ', '-'));

        var harness = new MediaToolHarness
        {
            Configure = collection => collection.AddSingleton(normalizer.Object),
        };

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ImportMediaFromUrlTool(), services, new()
        {
            ["url"] = "https://example.com/Company%20Logo.PNG",
        });

        // Assert
        Assert.Equal("company-logo.png", AgentToolTestContext.ReadJson(result).GetProperty("file").GetProperty("path").GetString());
    }

    private sealed class RefusingFileEventHandler : IFileEventHandler
    {
        private readonly string _reason;

        public RefusingFileEventHandler(string reason)
        {
            _reason = reason;
        }

        public Task<FileCreatingResult> CreatingAsync(FileCreatingContext context, Stream stream, CancellationToken cancellationToken = default)
            => Task.FromResult(FileCreatingResult.Failed(stream, new ResultError { Message = new LocalizedString(_reason, _reason) }));

        public Task CreatedAsync(IFileStoreEntry fileInfo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
