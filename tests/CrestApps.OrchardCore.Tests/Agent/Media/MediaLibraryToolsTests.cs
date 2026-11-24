using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Media;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.FileStorage;
using OrchardCore.Media;
using OrchardCore.Media.Indexing;

namespace CrestApps.OrchardCore.Tests.Agent.Media;

/// <summary>
/// The tools that list, read, move, copy and delete the files of the media library. They go through the media store the Media Library uses, check the folder permission on
/// every path, and a refused call changes nothing.
/// </summary>
public sealed class MediaLibraryToolsTests
{
    [Fact]
    public async Task ListMediaFiles_ReturnsTheSubFoldersAndTheFilesOfTheFolder()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFolder("archive")
            .AddFile("brand/logo.png", "12345")
            .AddFile("brand/guide.pdf")
            .AddFile("brand/icons/home.png")
            .AddFile("readme.txt");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMediaFilesTool(), services, new()
        {
            ["path"] = "/brand/",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var items = json.GetProperty("items").EnumerateArray().ToList();
        var folder = Assert.Single(json.GetProperty("folders").EnumerateArray());

        Assert.Equal("brand", json.GetProperty("path").GetString());
        Assert.Equal("brand/icons", folder.GetProperty("path").GetString());
        Assert.Equal("icons", folder.GetProperty("name").GetString());
        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.Equal(1, json.GetProperty("page").GetInt32());
        Assert.Equal(20, json.GetProperty("pageSize").GetInt32());
        Assert.Equal("UTC", json.GetProperty("timeZone").GetString());
        Assert.False(json.TryGetProperty("truncated", out _));
        Assert.Equal(new[] { "brand/guide.pdf", "brand/logo.png" }, items.Select(item => item.GetProperty("path").GetString()));

        var logo = items[1];

        Assert.Equal("logo.png", logo.GetProperty("name").GetString());
        Assert.Equal("brand", logo.GetProperty("folder").GetString());
        Assert.Equal(5, logo.GetProperty("size").GetInt64());
        Assert.Equal("image/png", logo.GetProperty("mimeType").GetString());
        Assert.Equal("2026-11-16T09:30:00+00:00", logo.GetProperty("lastModified").GetString());
        Assert.Equal("/media/brand/logo.png", logo.GetProperty("url").GetString());
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task ListMediaFiles_WithoutAPath_ListsTheRootFolder()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("brand/logo.png")
            .AddFile("readme.txt");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMediaFilesTool(), services);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(string.Empty, json.GetProperty("path").GetString());
        Assert.Equal("brand", Assert.Single(json.GetProperty("folders").EnumerateArray()).GetProperty("path").GetString());
        Assert.Equal("readme.txt", Assert.Single(json.GetProperty("items").EnumerateArray()).GetProperty("path").GetString());
    }

    [Fact]
    public async Task ListMediaFiles_WithANameAndExtensions_KeepsTheMatchingFiles()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("blog/Hero-Large.PNG")
            .AddFile("blog/hero-small.jpg")
            .AddFile("blog/hero.pdf")
            .AddFile("blog/footer.png");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMediaFilesTool(), services, new()
        {
            ["path"] = "blog",
            ["name"] = "HERO",
            ["extensions"] = new[] { "png", ".jpg" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(
            new[] { "blog/Hero-Large.PNG", "blog/hero-small.jpg" },
            json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("path").GetString()));
    }

    [Fact]
    public async Task ListMediaFiles_Recursive_ListsOnlyTheFoldersTheCallerMayManage()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("a.png")
            .AddFile("open/b.png")
            .AddFile("open/deep/c.png")
            .AddFile("_Users/other/secret.png");

        harness.RefuseFolder("_Users");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMediaFilesTool(), services, new()
        {
            ["recursive"] = true,
            ["page"] = 1,
            ["pageSize"] = 2,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(2, json.GetProperty("pageSize").GetInt32());
        Assert.Equal(new[] { "a.png", "open/b.png" }, json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("path").GetString()));
        Assert.Equal("open", Assert.Single(json.GetProperty("folders").EnumerateArray()).GetProperty("path").GetString());
        Assert.DoesNotContain("secret", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListMediaFiles_ForAFolderThatDoesNotExist_SaysSo()
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMediaFilesTool(), services, new()
        {
            ["path"] = "missing",
        });

        // Assert
        Assert.StartsWith("Nothing was listed: there is no media folder 'missing'.", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../App_Data")]
    [InlineData("brand/../../secrets")]
    [InlineData("C:/Windows")]
    [InlineData("brand/%2e%2e/secrets")]
    [InlineData("_Users/alice%41")]
    [InlineData("brand//logo.png")]
    public async Task ListMediaFiles_WithAPathThatIsNotAMediaPath_IsRefusedBeforeTheStoreIsRead(string path)
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListMediaFilesTool(), services, new()
        {
            ["path"] = path,
        });

        // Assert
        Assert.StartsWith("Nothing was listed:", result, StringComparison.Ordinal);
        Assert.Contains("is not a media path", result, StringComparison.Ordinal);

        // Only the permission to use the media library was checked: the path never reached the folder check.
        Assert.Equal("ManageMediaContent", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    [Fact]
    public async Task GetMediaFile_ReturnsTheDetailsOfTheFile()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFile("brand/logo.png", "12345");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = "brand/logo.png",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var file = json.GetProperty("file");

        Assert.Equal("brand/logo.png", file.GetProperty("path").GetString());
        Assert.Equal(5, file.GetProperty("size").GetInt64());
        Assert.Equal("image/png", file.GetProperty("mimeType").GetString());
        Assert.Equal("/media/brand/logo.png", file.GetProperty("url").GetString());
        Assert.False(json.TryGetProperty("text", out _));
        Assert.False(json.TryGetProperty("textNote", out _));
    }

    [Fact]
    public async Task GetMediaFile_WithIncludeText_ReadsASmallPlainTextFileAndCutsALongOne()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("notes/short.txt", "Opening hours: 9 to 5.")
            .AddFile("notes/long.txt", new string('a', GetMediaFileTool.MaxTextLength + 500));

        using var services = harness.Build();

        // Act
        var shortResult = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = "notes/short.txt",
            ["includeText"] = true,
        });

        var longResult = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = "notes/long.txt",
            ["includeText"] = true,
        });

        // Assert
        var shortJson = AgentToolTestContext.ReadJson(shortResult);
        var longJson = AgentToolTestContext.ReadJson(longResult);

        Assert.Equal("Opening hours: 9 to 5.", shortJson.GetProperty("text").GetString());
        Assert.False(shortJson.TryGetProperty("textTruncated", out _));
        Assert.Equal(GetMediaFileTool.MaxTextLength, longJson.GetProperty("text").GetString().Length);
        Assert.True(longJson.GetProperty("textTruncated").GetBoolean());
    }

    [Fact]
    public async Task GetMediaFile_WithIncludeText_UsesTheTextProviderOfTheMediaIndexingFeature()
    {
        // Arrange
        var harness = new MediaToolHarness();
        var indexingOptions = new MediaFileIndexingOptions();

        indexingOptions.RegisterMediaFileTextProvider<UpperCasePathTextProvider>(".pdf");

        harness.Store.AddFile("docs/terms.pdf", "%PDF-binary");
        harness.Configure = collection => collection.AddSingleton(Options.Create(indexingOptions));

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = "docs/terms.pdf",
            ["includeText"] = true,
        });

        // Assert
        Assert.Equal("TEXT OF DOCS/TERMS.PDF", AgentToolTestContext.ReadJson(result).GetProperty("text").GetString());
    }

    [Fact]
    public async Task GetMediaFile_WithIncludeText_ForAFileWithoutReadableText_ReturnsTheDetailsAndSaysWhy()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFile("brand/logo.png");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = "brand/logo.png",
            ["includeText"] = true,
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal("brand/logo.png", json.GetProperty("file").GetProperty("path").GetString());
        Assert.False(json.TryGetProperty("text", out _));
        Assert.Contains("'.png' file cannot be read", json.GetProperty("textNote").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMediaFile_ForAFileWhoseNameHoldsAPercentSign_FindsTheFileAsItIsStored()
    {
        // Arrange
        var harness = new MediaToolHarness();

        // A file that was saved from a web address and uploaded keeps the encoded name.
        harness.Store.AddFile("docs/Annual%20Report.pdf");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = "docs/Annual%20Report.pdf",
        });

        // Assert
        Assert.Equal("docs/Annual%20Report.pdf", AgentToolTestContext.ReadJson(result).GetProperty("file").GetProperty("path").GetString());
        Assert.Contains(("ManageMediaFolder", (object)"docs/Annual%20Report.pdf"), services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Theory]
    [InlineData("docs/..%2fsecret.pdf")]
    [InlineData("docs/%2e%2e")]
    [InlineData("docs/%2e%2e/secret.pdf")]
    [InlineData("_Users/alice%41/photo.png")]
    [InlineData("docs/C%3asecret.pdf")]
    [InlineData("docs/a%00b.pdf")]
    [InlineData("docs/../secret.pdf")]
    [InlineData("")]
    public async Task GetMediaFile_WithAPathThatReadsDifferentlyWhenDecoded_IsRefusedBeforeTheFolderCheck(string path)
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = path,
        });

        // Assert
        // The permission check decodes a path and the store does not, so such a path could be checked in one folder and
        // read in another.
        Assert.DoesNotContain("\"file\"", result, StringComparison.Ordinal);
        Assert.Equal("ManageMediaContent", Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
    }

    [Fact]
    public async Task GetMediaFile_ForAFileThatDoesNotExist_SaysSo()
    {
        // Arrange
        var harness = new MediaToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new GetMediaFileTool(), services, new()
        {
            ["path"] = "brand/missing.png",
        });

        // Assert
        Assert.StartsWith("There is no media file 'brand/missing.png'.", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MoveMediaFiles_MovesAndRenamesTheFilesAndSaysThatReferencesAreNotUpdated()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFolder("archive")
            .AddFile("brand/logo-old.png", "logo")
            .AddFile("tmp/photo.jpg", "photo");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["action"] = "move",
            ["files"] = new JsonArray(
                new JsonObject { ["path"] = "brand/logo-old.png", ["newPath"] = "brand/logo-2025.png" },
                new JsonObject { ["path"] = "tmp/photo.jpg", ["newPath"] = "archive/photo.jpg" }),
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var moved = json.GetProperty("files").EnumerateArray().ToList();

        Assert.Equal("move", json.GetProperty("action").GetString());
        Assert.Equal(2, json.GetProperty("count").GetInt32());
        Assert.Equal("brand/logo-old.png", moved[0].GetProperty("from").GetString());
        Assert.Equal("brand/logo-2025.png", moved[0].GetProperty("to").GetString());
        Assert.Equal("/media/archive/photo.jpg", moved[1].GetProperty("url").GetString());
        Assert.False(json.TryGetProperty("failed", out _));
        Assert.Contains("not updated", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "move:brand/logo-old.png>brand/logo-2025.png", "move:tmp/photo.jpg>archive/photo.jpg" }, harness.Store.Changes);
        Assert.Equal("logo", harness.Store.ReadText("brand/logo-2025.png"));
        Assert.Null(harness.Store.ReadText("brand/logo-old.png"));
    }

    [Theory]
    [InlineData("brand/missing.png", "brand/new.png", "There is no media file 'brand/missing.png'.")]
    [InlineData("brand/logo.png", "brand/taken.png", "A media file already exists at 'brand/taken.png'.")]
    [InlineData("brand/logo.png", "nowhere/logo.png", "There is no media folder 'nowhere'.")]
    [InlineData("brand/logo.png", "brand/logo.exe", "the extension '.exe' is not allowed on this site")]
    [InlineData("brand/logo.png", "brand/logo.png", "has the same old and new path")]
    public async Task MoveMediaFiles_WhenOneFileCannotBeMoved_MovesNothing(string path, string newPath, string expected)
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("brand/logo.png")
            .AddFile("brand/taken.png")
            .AddFile("brand/other.png");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["files"] = $$"""[{ "path": "brand/other.png", "newPath": "brand/fine.png" }, { "path": "{{path}}", "newPath": "{{newPath}}" }]""",
        });

        // Assert
        Assert.StartsWith("Nothing was moved:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task MoveMediaFiles_ToARestrictedExtension_NeedsThePermissionToUploadRestrictedMedia()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFile("brand/logo.png");
        harness.Refuse(MediaPermissions.UploadRestrictedMedia);

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["files"] = """[{ "path": "brand/logo.png", "newPath": "brand/logo.svg" }]""",
        });

        // Assert
        Assert.Contains("the extension '.svg' is restricted", result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task MoveMediaFiles_ChecksTheFolderPermissionOnTheOldAndTheNewPath()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFolder("mediafields/BlogPost")
            .AddFile("brand/logo.png");

        harness.RefuseFolder("mediafields");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["files"] = """[{ "path": "brand/logo.png", "newPath": "mediafields/BlogPost/logo.png" }]""",
        });

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Contains(("ManageMediaFolder", (object)"brand/logo.png"), checks);
        Assert.Contains(("ManageMediaFolder", (object)"mediafields/BlogPost/logo.png"), checks);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task MoveMediaFiles_WhenTheStoreFails_ReportsTheFileThatWasNotMoved()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFile("brand/logo.png");
        harness.Store.MoveError = "The storage account is read-only.";

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["files"] = """[{ "path": "brand/logo.png", "newPath": "brand/logo-new.png" }]""",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var failed = Assert.Single(json.GetProperty("failed").EnumerateArray());

        Assert.Equal(0, json.GetProperty("count").GetInt32());
        Assert.Equal("brand/logo.png", failed.GetProperty("from").GetString());
        Assert.Equal("The storage account is read-only.", failed.GetProperty("error").GetString());
        Assert.False(json.TryGetProperty("note", out _));
    }

    [Fact]
    public async Task MoveMediaFiles_ToCopy_CopiesTheFilesAndKeepsTheOriginals()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("brand/logo.png", "logo")
            .AddFile("brand/icon.png", "icon");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["action"] = "copy",
            ["files"] = """[{ "path": "brand/logo.png", "newPath": "brand/logo-copy.png" }, { "path": "brand/icon.png", "newPath": "brand/icon-copy.png" }]""",
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);
        var copied = json.GetProperty("files").EnumerateArray().ToList();

        Assert.Equal("copy", json.GetProperty("action").GetString());
        Assert.Equal(2, json.GetProperty("count").GetInt32());
        Assert.Equal("brand/logo.png", copied[0].GetProperty("from").GetString());
        Assert.Equal("brand/logo-copy.png", copied[0].GetProperty("to").GetString());
        Assert.Equal("/media/brand/logo-copy.png", copied[0].GetProperty("url").GetString());
        Assert.False(json.TryGetProperty("note", out _));
        Assert.Equal(new[] { "copy:brand/logo.png>brand/logo-copy.png", "copy:brand/icon.png>brand/icon-copy.png" }, harness.Store.Changes);
        Assert.Equal("logo", harness.Store.ReadText("brand/logo.png"));
        Assert.Equal("logo", harness.Store.ReadText("brand/logo-copy.png"));
    }

    [Theory]
    [InlineData("brand/missing.png", "brand/copy.png", "There is no media file 'brand/missing.png'.")]
    [InlineData("brand/logo.png", "brand/taken.png", "A media file already exists at 'brand/taken.png'.")]
    [InlineData("brand/logo.png", "brand/logo.exe", "the extension '.exe' is not allowed on this site")]
    [InlineData("brand/logo.png", "nowhere/logo.png", "There is no media folder 'nowhere'.")]
    public async Task MoveMediaFiles_WhenACopyCannotBeMade_CopiesNothing(string path, string newPath, string expected)
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("brand/logo.png")
            .AddFile("brand/taken.png");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["action"] = "copy",
            ["files"] = $$"""[{ "path": "{{path}}", "newPath": "{{newPath}}" }]""",
        });

        // Assert
        Assert.StartsWith("Nothing was copied:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task MoveMediaFiles_ForAnUnknownAction_ChangesNothing()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store.AddFile("brand/logo.png");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["action"] = "link",
            ["files"] = """[{ "path": "brand/logo.png", "newPath": "brand/logo-new.png" }]""",
        });

        // Assert
        Assert.Equal("Nothing was changed: 'link' is not an action. Use move or copy.", result);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task DeleteMedia_DeletesTheFilesAndSaysTheyCannotBeRestored()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("tmp/a.png")
            .AddFile("tmp/b.png")
            .AddFile("tmp/keep.png");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteMediaTool(), services, new()
        {
            ["paths"] = new[] { "tmp/a.png", "/tmp/b.png", "tmp/a.png" },
        });

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.Equal(new[] { "tmp/a.png", "tmp/b.png" }, json.GetProperty("deleted").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(2, json.GetProperty("deletedCount").GetInt32());
        Assert.Contains("cannot be restored", json.GetProperty("note").GetString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "delete:tmp/a.png", "delete:tmp/b.png" }, harness.Store.Changes);
        Assert.NotNull(harness.Store.ReadText("tmp/keep.png"));
    }

    [Fact]
    public async Task DeleteMedia_WhenAPathIsNotAFile_DeletesNothing()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("tmp/a.png")
            .AddFolder("tmp/old");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteMediaTool(), services, new()
        {
            ["paths"] = new[] { "tmp/a.png", "tmp/missing.png", "tmp/old" },
        });

        // Assert
        Assert.StartsWith("Nothing was deleted:", result, StringComparison.Ordinal);
        Assert.Contains("There is no media file 'tmp/missing.png'.", result, StringComparison.Ordinal);
        Assert.Contains("'tmp/old' is a folder", result, StringComparison.Ordinal);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task DeleteMedia_WhenOnePathIsInAFolderTheCallerMayNotManage_DeletesNothing()
    {
        // Arrange
        var harness = new MediaToolHarness();

        harness.Store
            .AddFile("tmp/a.png")
            .AddFile("_Users/other/photo.png");

        harness.RefuseFolder("_Users");

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteMediaTool(), services, new()
        {
            ["paths"] = new[] { "tmp/a.png", "_Users/other/photo.png" },
        });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Contains(("ManageMediaFolder", (object)"_Users/other/photo.png"), services.GetRequiredService<FakeAuthorizationService>().Checks);
        Assert.Empty(harness.Store.Changes);
    }

    [Fact]
    public async Task MoveMediaFiles_OnARemoteStore_CopiesTheMovedFileToTheLocalCache()
    {
        // Arrange
        var cache = new Mock<IMediaFileStoreCache>();
        var harness = new MediaToolHarness
        {
            Configure = collection => collection.AddSingleton(cache.Object),
        };

        harness.Store.AddFile("brand/logo.png");

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new MoveMediaFilesTool(), services, new()
        {
            ["files"] = """[{ "path": "brand/logo.png", "newPath": "brand/logo-new.png" }]""",
        });

        // Assert
        cache.Verify(
            mediaCache => mediaCache.SetCacheAsync(It.IsAny<Stream>(), It.Is<IFileStoreEntry>(file => file.Path == "brand/logo-new.png"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class UpperCasePathTextProvider : IMediaFileTextProvider
    {
        public Task<string> GetTextAsync(string path, Stream fileStream)
            => Task.FromResult($"TEXT OF {path.ToUpperInvariant()}");
    }
}
