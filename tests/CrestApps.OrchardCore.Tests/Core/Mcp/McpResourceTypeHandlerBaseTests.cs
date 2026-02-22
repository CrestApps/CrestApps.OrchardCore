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
    public async Task ReadAsync_WithNullVariables_ThrowsArgumentNullException()
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
        var variables = new Dictionary<string, string>();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.ReadAsync(null, variables, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadAsync_WithValidInputs_DelegatesToProtectedMethod()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource("test://item1/some/path");
        var variables = new Dictionary<string, string>
        {
            ["path"] = "some/path",
        };

        var result = await handler.ReadAsync(resource, variables, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(handler.LastVariables);
        Assert.Equal("some/path", handler.LastVariables["path"]);
    }

    [Fact]
    public async Task ReadAsync_WithEmptyVariables_DelegatesToProtectedMethod()
    {
        var handler = new TestHandler("test");
        var resource = CreateResource("test://item1/some/path");
        var variables = new Dictionary<string, string>();

        var result = await handler.ReadAsync(resource, variables, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(handler.LastVariables);
        Assert.Empty(handler.LastVariables);
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
        public IReadOnlyDictionary<string, string> LastVariables { get; private set; }

        public TestHandler(string type) : base(type)
        {
        }

        protected override Task<ReadResourceResult> GetResultAsync(McpResource resource, IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken)
        {
            LastVariables = variables;

            return Task.FromResult(new ReadResourceResult
            {
                Contents = [new TextResourceContents { Uri = resource.Resource.Uri, Text = "ok" }],
            });
        }
    }
}
