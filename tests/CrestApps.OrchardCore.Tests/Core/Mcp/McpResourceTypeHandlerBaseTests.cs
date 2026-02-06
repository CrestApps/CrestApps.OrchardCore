using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using ModelContextProtocol.Protocol;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class McpResourceTypeHandlerBaseTests
{
    [Fact]
    public void Constructor_SetsTypeProperty()
    {
        var handler = new TestHandler("test-type");

        Assert.Equal("test-type", handler.Type);
    }

    [Fact]
    public void Constructor_WithNullType_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TestHandler(null));
    }

    [Fact]
    public void Constructor_WithEmptyType_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TestHandler(string.Empty));
    }

    [Fact]
    public async Task ReadAsync_WithNullResourceUri_ThrowsArgumentNullException()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource("test://item1/some/path");

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.ReadAsync(resource, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_WithNullResource_ThrowsArgumentNullException()
    {
        var handler = new TestHandler("test");

        _ = McpResourceUri.TryParse("test://item1/some/path", out var resourceUri);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.ReadAsync(null, resourceUri, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_WithValidUri_DelegatesToProtectedMethod()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource("test://item1/some/path");

        _ = McpResourceUri.TryParse("test://item1/some/path", out var resourceUri);

        var result = await handler.ReadAsync(resource, resourceUri, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(handler.LastResourceUri);
        Assert.Equal("test", handler.LastResourceUri.Scheme);
        Assert.Equal("item1", handler.LastResourceUri.ItemId);
        Assert.Equal("some/path", handler.LastResourceUri.Path);
    }

    private static McpResource CreateResource(string uri)
    {
        return new McpResource
        {
            ItemId = "item1",
            Source = "test",
            Resource = new Resource
            {
                Uri = uri,
                Name = "test-resource",
            },
        };
    }

    private sealed class TestHandler : McpResourceTypeHandlerBase
    {
        public McpResourceUri LastResourceUri { get; private set; }

        public TestHandler(string type) : base(type)
        {
        }

        protected override Task<ReadResourceResult> GetResultAsync(McpResource resource, McpResourceUri resourceUri, CancellationToken cancellationToken)
        {
            LastResourceUri = resourceUri;

            return Task.FromResult(new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = resource.Resource.Uri, Text = "ok" }],
            });
        }
    }
}
