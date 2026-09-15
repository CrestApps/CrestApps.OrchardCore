using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The inbound processor took the first registered <see cref="IEntryPointResolver"/> and ignored the rest, so a
/// second one — the way a feature would add its own entry-point source — was silently never asked. A number it
/// alone knew about resolved to nothing, and the caller was routed as though the tenant had never configured it.
/// Either it is one service or it is a chain; this makes it a chain, first plan wins.
/// </summary>
public sealed class EntryPointResolverChainTests
{
    [Fact]
    public async Task Resolve_AsksResolversInOrder_AndTakesTheFirstPlan()
    {
        // Arrange
        var chain = new EntryPointResolverChain(
        [
            new StubResolver(order: 200, plan: new EntryPointRoutingPlan { TargetQueueId = "second" }),
            new StubResolver(order: 100, plan: new EntryPointRoutingPlan { TargetQueueId = "first" }),
        ]);

        // Act
        var plan = await chain.ResolveAsync("+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("first", plan.TargetQueueId);
    }

    [Fact]
    public async Task Resolve_FallsThroughAResolverThatKnowsNothingAboutTheNumber()
    {
        // Arrange
        // This is the case that was broken: the number belongs to the second resolver, and the first returning
        // null used to end the search.
        var chain = new EntryPointResolverChain(
        [
            new StubResolver(order: 100, plan: null),
            new StubResolver(order: 200, plan: new EntryPointRoutingPlan { TargetQueueId = "second" }),
        ]);

        // Act
        var plan = await chain.ResolveAsync("+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("second", plan.TargetQueueId);
    }

    [Fact]
    public async Task Resolve_ReturnsNothing_WhenNoResolverKnowsTheNumber()
    {
        // Arrange
        var chain = new EntryPointResolverChain([new StubResolver(order: 100, plan: null)]);

        // Act
        var plan = await chain.ResolveAsync("+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(plan);
    }

    [Fact]
    public async Task Resolve_WithNoResolversAtAll_ReturnsNothing()
    {
        // Arrange
        // A tenant with inbound voice but no entry points configured is a normal state, not an error.
        var chain = new EntryPointResolverChain([]);

        // Act
        var plan = await chain.ResolveAsync("+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(plan);
    }

    [Fact]
    public async Task Find_UsesTheSameChain_SoLookupAndRoutingAgree()
    {
        // Arrange
        // If the entry point lookup and the plan resolution disagreed about which resolver owns a number, a call
        // would be recorded against one entry point and routed by another.
        var chain = new EntryPointResolverChain(
        [
            new StubResolver(order: 100, plan: null),
            new StubResolver(order: 200, plan: new EntryPointRoutingPlan { TargetQueueId = "second" }, entryPoint: new ContactCenterEntryPoint { ItemId = "ep-2" }),
        ]);

        // Act
        var entryPoint = await chain.FindByDialedNumberAsync("+16502530000", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ep-2", entryPoint.ItemId);
    }

    private sealed class StubResolver : IEntryPointResolver, IOrderedEntryPointResolver
    {
        private readonly EntryPointRoutingPlan _plan;
        private readonly ContactCenterEntryPoint _entryPoint;

        public StubResolver(int order, EntryPointRoutingPlan plan, ContactCenterEntryPoint entryPoint = null)
        {
            Order = order;
            _plan = plan;
            _entryPoint = entryPoint;
        }

        public int Order { get; }

        public Task<ContactCenterEntryPoint> FindByDialedNumberAsync(string dialedNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_entryPoint);

        public Task<EntryPointRoutingPlan> ResolveAsync(string dialedNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_plan);
    }
}
