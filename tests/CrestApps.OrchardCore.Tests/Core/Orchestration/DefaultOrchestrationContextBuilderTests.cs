using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Core.Orchestration;

public sealed class DefaultOrchestrationContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_NullResource_Throws()
    {
        var builder = CreateBuilder([]);

        await Assert.ThrowsAsync<ArgumentNullException>(() => builder.BuildAsync(null).AsTask());
    }

    [Fact]
    public async Task BuildAsync_NoHandlers_ReturnsEmptyContext()
    {
        var builder = CreateBuilder([]);
        var resource = new AIProfile { DisplayText = "Test" };

        var context = await builder.BuildAsync(resource);

        Assert.NotNull(context);
        Assert.Null(context.UserMessage);
        Assert.Null(context.SourceName);
        Assert.Empty(context.ConversationHistory);
        Assert.Empty(context.Documents);
    }

    [Fact]
    public async Task BuildAsync_HandlerCanPopulateContext()
    {
        var handler = new TestHandler(
            building: ctx => ctx.Context.SourceName = "test-source",
            built: null);
        var builder = CreateBuilder([handler]);

        var context = await builder.BuildAsync(new AIProfile());

        Assert.Equal("test-source", context.SourceName);
    }

    [Fact]
    public async Task BuildAsync_ConfigureDelegateRunsAfterBuilding()
    {
        var order = new List<string>();

        var handler = new TestHandler(
            building: ctx => order.Add("building"),
            built: ctx => order.Add("built"));
        var builder = CreateBuilder([handler]);

        var context = await builder.BuildAsync(new AIProfile(), ctx =>
        {
            order.Add("configure");
            ctx.UserMessage = "Hello";
        });

        Assert.Equal(["building", "configure", "built"], order);
        Assert.Equal("Hello", context.UserMessage);
    }

    [Fact]
    public async Task BuildAsync_ConfigureDelegateCanOverrideHandler()
    {
        var handler = new TestHandler(
            building: ctx => ctx.Context.SourceName = "handler-source",
            built: null);
        var builder = CreateBuilder([handler]);

        var context = await builder.BuildAsync(new AIProfile(), ctx =>
        {
            ctx.SourceName = "override-source";
        });

        Assert.Equal("override-source", context.SourceName);
    }

    [Fact]
    public async Task BuildAsync_HandlersExecuteInReverseOrder()
    {
        var order = new List<string>();

        var handler1 = new TestHandler(
            building: ctx => order.Add("handler1"),
            built: null);
        var handler2 = new TestHandler(
            building: ctx => order.Add("handler2"),
            built: null);

        // Handlers are reversed internally, so last registered runs first.
        var builder = CreateBuilder([handler1, handler2]);

        await builder.BuildAsync(new AIProfile());

        // Reverse of [handler1, handler2] = [handler2, handler1]
        Assert.Equal(["handler2", "handler1"], order);
    }

    [Fact]
    public async Task BuildAsync_PropertiesBagIsAccessible()
    {
        var handler = new TestHandler(
            building: ctx => ctx.Context.Properties["key"] = "value",
            built: null);
        var builder = CreateBuilder([handler]);

        var context = await builder.BuildAsync(new AIProfile());

        Assert.Equal("value", context.Properties["key"]);
    }

    [Fact]
    public async Task BuildAsync_BuiltHandlerSeesConfigureChanges()
    {
        string capturedMessage = null;

        var handler = new TestHandler(
            building: null,
            built: ctx =>
            {
                capturedMessage = ((OrchestrationContext)ctx.Context).UserMessage;
            });
        var builder = CreateBuilder([handler]);

        await builder.BuildAsync(new AIProfile(), ctx =>
        {
            ctx.UserMessage = "After configure";
        });

        Assert.Equal("After configure", capturedMessage);
    }

    private static DefaultOrchestrationContextBuilder CreateBuilder(IEnumerable<IOrchestrationContextHandler> handlers)
    {
        return new DefaultOrchestrationContextBuilder(handlers, new EmptyServiceProvider(), NullLogger<DefaultOrchestrationContextBuilder>.Instance);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType) => null;
    }

    private sealed class TestHandler : IOrchestrationContextHandler
    {
        private readonly Action<OrchestrationContextBuildingContext> _building;
        private readonly Action<OrchestrationContextBuiltContext> _built;

        public TestHandler(
            Action<OrchestrationContextBuildingContext> building,
            Action<OrchestrationContextBuiltContext> built)
        {
            _building = building;
            _built = built;
        }

        public Task BuildingAsync(OrchestrationContextBuildingContext context)
        {
            _building?.Invoke(context);
            return Task.CompletedTask;
        }

        public Task BuiltAsync(OrchestrationContextBuiltContext context)
        {
            _built?.Invoke(context);
            return Task.CompletedTask;
        }
    }
}
