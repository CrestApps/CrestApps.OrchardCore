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
    public async Task ReadAsync_WithNullUri_ReturnsErrorResult()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource(null);

        var result = await handler.ReadAsync(resource, TestContext.Current.CancellationToken);

        var textContent = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("text/plain", textContent.MimeType);
        Assert.Equal("Resource URI is required.", textContent.Text);
    }

    [Fact]
    public async Task ReadAsync_WithEmptyUri_ReturnsErrorResult()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource(string.Empty);

        var result = await handler.ReadAsync(resource, TestContext.Current.CancellationToken);

        var textContent = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("text/plain", textContent.MimeType);
        Assert.Equal("Resource URI is required.", textContent.Text);
    }

    [Fact]
    public async Task ReadAsync_WithInvalidUri_ReturnsErrorResult()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource("not-a-valid-uri");

        var result = await handler.ReadAsync(resource, TestContext.Current.CancellationToken);

        var textContent = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("text/plain", textContent.MimeType);
        Assert.Contains("Invalid test URI", textContent.Text);
    }

    [Fact]
    public async Task ReadAsync_WithNullResource_ReturnsErrorResult()
    {
        var handler = new TestHandler("test");
        var mcpResource = new McpResource
        {
            ItemId = "item1",
            Source = "test",
            Resource = null,
        };

        var result = await handler.ReadAsync(mcpResource, TestContext.Current.CancellationToken);

        var textContent = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("text/plain", textContent.MimeType);
        Assert.Equal("Resource URI is required.", textContent.Text);
    }

    [Fact]
    public async Task ReadAsync_WithValidUri_DelegatesToProtectedMethod()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource("test://item1/some/path");

        var result = await handler.ReadAsync(resource, TestContext.Current.CancellationToken);

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
