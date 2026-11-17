using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Media;
using CrestApps.OrchardCore.AI.Agent.Shortcodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Tests.Agent.Media;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Media;
using OrchardCore.Shortcodes.Services;

namespace CrestApps.OrchardCore.Tests.Agent;

/// <summary>
/// Every tool for the media library, the media cache and the shortcode catalog acts for the person in the chat: it
/// refuses a caller who is not signed in and a caller who does not hold the permission the matching admin action asks
/// for, and it changes nothing when it refuses. The media file tools also check the folder permission with the path as
/// the resource, as the Media Library does. Each JSON schema takes no argument it does not declare and describes each
/// argument.
/// </summary>
public sealed class OrchardMediaToolCallerTests
{
    public static TheoryData<string> ToolNames =>
    [
        ListMediaFilesTool.TheName,
        GetMediaFileTool.TheName,
        GetMediaOptionsTool.TheName,
        ImportMediaFromUrlTool.TheName,
        SaveMediaTextFileTool.TheName,
        MoveMediaFileTool.TheName,
        CopyMediaFileTool.TheName,
        DeleteMediaFileTool.TheName,
        PurgeMediaCacheTool.TheName,
        ListShortcodesTool.TheName,
    ];

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesAndChangesNothing(string toolName)
    {
        // Arrange
        var doubles = new Doubles(signedIn: false);

        using var services = doubles.BuildServices();

        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        doubles.VerifyNothingChangedOrFetched();
    }

    [Theory]
    [InlineData(ListMediaFilesTool.TheName, "ManageMediaContent")]
    [InlineData(GetMediaFileTool.TheName, "ManageMediaContent")]
    [InlineData(GetMediaOptionsTool.TheName, "ViewMediaOptions")]
    [InlineData(ImportMediaFromUrlTool.TheName, "ManageMediaContent")]
    [InlineData(SaveMediaTextFileTool.TheName, "ManageMediaContent")]
    [InlineData(MoveMediaFileTool.TheName, "ManageMediaContent")]
    [InlineData(CopyMediaFileTool.TheName, "ManageMediaContent")]
    [InlineData(DeleteMediaFileTool.TheName, "ManageMediaContent")]
    [InlineData(PurgeMediaCacheTool.TheName, "ManageAssetCache")]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminAction_RefusesAndChangesNothing(string toolName, string permission)
    {
        // Arrange
        var doubles = new Doubles(signedIn: true);

        doubles.Harness.Authorize = (_, _) => false;

        using var services = doubles.BuildServices();

        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var check = Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks);

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, check.Permission);
        Assert.Null(check.Resource);
        doubles.VerifyNothingChangedOrFetched();
    }

    [Theory]
    [InlineData(ListMediaFilesTool.TheName, "brand")]
    [InlineData(GetMediaFileTool.TheName, "brand/logo.png")]
    [InlineData(ImportMediaFromUrlTool.TheName, "brand")]
    [InlineData(SaveMediaTextFileTool.TheName, "brand")]
    [InlineData(MoveMediaFileTool.TheName, "brand/logo.png")]
    [InlineData(CopyMediaFileTool.TheName, "brand/logo.png")]
    [InlineData(DeleteMediaFileTool.TheName, "brand/logo.png")]
    public async Task InvokeAsync_WhenTheCallerMayNotManageTheFolder_RefusesAndChangesNothing(string toolName, string resource)
    {
        // Arrange
        var doubles = new Doubles(signedIn: true);

        // The caller may use the media library, but not this folder.
        doubles.Harness.Refuse(MediaPermissions.ManageMediaFolder);

        using var services = doubles.BuildServices();

        var (tool, arguments) = CreateTool(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(("ManageMediaContent", (object)null), checks[0]);
        Assert.Equal(("ManageMediaFolder", (object)resource), checks[1]);
        Assert.Equal(2, checks.Count);
        doubles.VerifyNothingChangedOrFetched();
    }

    [Fact]
    public async Task ListShortcodes_ForASignedInCaller_ChecksNoPermissionAsTheShortcodePickerDoes()
    {
        // Arrange
        var doubles = new Doubles(signedIn: true);

        doubles.Harness.Authorize = (_, _) => false;

        using var services = doubles.BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListShortcodesTool(), services);

        // Assert
        Assert.Equal(0, AgentToolTestContext.ReadJson(result).GetProperty("total").GetInt32());
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public void JsonSchema_ClosesTheArgumentsAndDescribesEachOne(string toolName)
    {
        // Arrange
        var (tool, _) = CreateTool(toolName);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        Assert.Equal(toolName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        AssertClosedAndDescribed(schema);
    }

    private static void AssertClosedAndDescribed(JsonElement schema)
    {
        Assert.Equal(JsonValueKind.False, schema.GetProperty("additionalProperties").ValueKind);
        Assert.Equal(JsonValueKind.Array, schema.GetProperty("required").ValueKind);

        var properties = schema.GetProperty("properties").EnumerateObject().ToList();

        Assert.All(properties, property => Assert.False(string.IsNullOrWhiteSpace(property.Value.GetProperty("description").GetString())));
        Assert.All(schema.GetProperty("required").EnumerateArray(), required => Assert.Contains(properties, property => property.Name == required.GetString()));

        // The objects of an array argument are closed and described too.
        foreach (var property in properties)
        {
            if (property.Value.TryGetProperty("items", out var items) && items.GetProperty("type").GetString() == "object")
            {
                AssertClosedAndDescribed(items);
            }
        }
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) CreateTool(string toolName)
    {
        return toolName switch
        {
            ListMediaFilesTool.TheName => (new ListMediaFilesTool(), new() { ["path"] = "brand" }),
            GetMediaFileTool.TheName => (new GetMediaFileTool(), new() { ["path"] = "brand/logo.png", ["includeText"] = true }),
            GetMediaOptionsTool.TheName => (new GetMediaOptionsTool(), []),
            ImportMediaFromUrlTool.TheName => (new ImportMediaFromUrlTool(), new() { ["url"] = "https://example.com/new.png", ["folder"] = "brand" }),
            SaveMediaTextFileTool.TheName => (new SaveMediaTextFileTool(), new() { ["path"] = "brand/notes.txt", ["text"] = "notes" }),
            MoveMediaFileTool.TheName => (new MoveMediaFileTool(), new() { ["files"] = """[{ "path": "brand/logo.png", "newPath": "brand/logo-new.png" }]""" }),
            CopyMediaFileTool.TheName => (new CopyMediaFileTool(), new() { ["path"] = "brand/logo.png", ["newPath"] = "brand/logo-copy.png" }),
            DeleteMediaFileTool.TheName => (new DeleteMediaFileTool(), new() { ["paths"] = new[] { "brand/logo.png" } }),
            PurgeMediaCacheTool.TheName => (new PurgeMediaCacheTool(), []),
            ListShortcodesTool.TheName => (new ListShortcodesTool(), []),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, "The tool has no test arguments."),
        };
    }

    /// <summary>
    /// The services the tools use, with a media library that holds one file. The media cache and the shortcode catalog
    /// are strict, the media store records every change and the downloader records every request, so a refused call
    /// that still read the shortcodes, changed a file, purged the cache or fetched an address fails the test.
    /// </summary>
    private sealed class Doubles
    {
        private readonly Mock<IMediaFileStoreCache> _mediaFileStoreCache = new(MockBehavior.Strict);
        private readonly Mock<IShortcodeDescriptorManager> _shortcodeDescriptorManager = new();

        public Doubles(bool signedIn)
        {
            Harness.SignedIn = signedIn;
            Harness.Store.AddFile("brand/logo.png");
            Harness.Configure = services =>
            {
                services.AddSingleton(_mediaFileStoreCache.Object);
                services.AddSingleton(_shortcodeDescriptorManager.Object);
            };

            _shortcodeDescriptorManager.Setup(manager => manager.GetShortcodeDescriptors()).ReturnsAsync([]);
        }

        public MediaToolHarness Harness { get; } = new();

        public ServiceProvider BuildServices()
            => Harness.Build();

        public void VerifyNothingChangedOrFetched()
        {
            Assert.Empty(Harness.Store.Changes);
            Assert.Empty(Harness.Requests);
            _mediaFileStoreCache.VerifyNoOtherCalls();
            _shortcodeDescriptorManager.VerifyNoOtherCalls();
        }
    }
}
