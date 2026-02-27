using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// Tests that <see cref="AIInvocationScope"/> provides true per-invocation isolation
/// using <see cref="AsyncLocal{T}"/>, even under concurrent multi-threaded execution.
///
/// <para>
/// <b>How AsyncLocal works:</b>
/// <c>AsyncLocal&lt;T&gt;</c> stores a value that is local to the current async control
/// flow. When you <c>await</c> a task, the runtime captures the current
/// <c>ExecutionContext</c> (which contains all AsyncLocal values) and restores it when
/// the continuation runs — even if the continuation runs on a different thread-pool
/// thread. Each fork of the control flow (e.g., <c>Task.Run</c> or <c>Task.WhenAll</c>)
/// gets its own copy of the <c>ExecutionContext</c>, so writes in one branch do not
/// affect another.
/// </para>
///
/// <para>
/// This is fundamentally different from <c>ThreadLocal&lt;T&gt;</c> (which is pinned to
/// the OS thread) and from <c>HttpContext.Items</c> (which is pinned to the HTTP
/// request/connection and shared by all code running on that connection).
/// </para>
/// </summary>
public sealed class AIInvocationScopeTests
{
    [Fact]
    public void Current_ReturnsNull_WhenNoScopeStarted()
    {
        // Outside any scope, Current should be null.
        Assert.Null(AIInvocationScope.Current);
    }

    [Fact]
    public void Begin_SetsCurrent_AndDisposeClears()
    {
        Assert.Null(AIInvocationScope.Current);

        using (var scope = AIInvocationScope.Begin())
        {
            Assert.NotNull(AIInvocationScope.Current);
            Assert.Same(scope.Context, AIInvocationScope.Current);
        }

        // After dispose, Current is null again.
        Assert.Null(AIInvocationScope.Current);
    }

    [Fact]
    public void Begin_WithExplicitContext_UsesProvidedInstance()
    {
        var context = new AIInvocationContext
        {
            DataSourceId = "test-ds-123"
        };

        using var scope = AIInvocationScope.Begin(context);

        Assert.Same(context, AIInvocationScope.Current);
        Assert.Equal("test-ds-123", AIInvocationScope.Current.DataSourceId);
    }

    [Fact]
    public async Task Current_FlowsAcrossAwait()
    {
        // AsyncLocal flows through await boundaries — even if the
        // continuation runs on a different thread-pool thread.
        using var scope = AIInvocationScope.Begin();
        var expected = scope.Context;

        expected.DataSourceId = "before-await";

        // Force a thread switch.
        await Task.Yield();

        Assert.Same(expected, AIInvocationScope.Current);
        Assert.Equal("before-await", AIInvocationScope.Current.DataSourceId);
    }

    [Fact]
    public async Task ConcurrentInvocations_AreFullyIsolated()
    {
        // Simulates two SignalR hub invocations happening at the same time.
        // Each should see its own AIInvocationContext and never the other's.
        const int invocationCount = 50;
        var barrier = new Barrier(invocationCount);
        var errors = new List<string>();

        var tasks = Enumerable.Range(0, invocationCount).Select(i => Task.Run(async () =>
        {
            // Each "hub invocation" starts its own scope.
            using var scope = AIInvocationScope.Begin();
            var myContext = scope.Context;
            myContext.DataSourceId = $"ds-{i}";

            // Synchronize all tasks to maximize overlap.
            barrier.SignalAndWait();

            // Simulate some async work (like AI completion streaming).
            await Task.Yield();

            // Verify our context is still ours.
            var current = AIInvocationScope.Current;

            if (current is null)
            {
                lock (errors) errors.Add($"Invocation {i}: Current was null after await.");
                return;
            }

            if (!ReferenceEquals(current, myContext))
            {
                lock (errors) errors.Add($"Invocation {i}: Current points to a different instance (got DataSourceId={current.DataSourceId}, expected ds-{i}).");
                return;
            }

            if (current.DataSourceId != $"ds-{i}")
            {
                lock (errors) errors.Add($"Invocation {i}: DataSourceId was '{current.DataSourceId}', expected 'ds-{i}'.");
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        Assert.Empty(errors);
    }

    [Fact]
    public void NestedScopes_DoNotLeakToOuterScope()
    {
        using var outerScope = AIInvocationScope.Begin();
        outerScope.Context.DataSourceId = "outer";

        using (var innerScope = AIInvocationScope.Begin())
        {
            innerScope.Context.DataSourceId = "inner";
            Assert.Equal("inner", AIInvocationScope.Current.DataSourceId);
        }

        // After inner scope disposes, outer scope's context should NOT be restored
        // because AsyncLocal Dispose sets it to null. This is the expected behavior:
        // each hub invocation should have exactly one scope.
        Assert.Null(AIInvocationScope.Current);
    }

    [Fact]
    public async Task ReferenceCounter_IsIsolatedPerInvocation()
    {
        // Two invocations incrementing their own counters should not interfere.
        const int invocationCount = 20;
        var barrier = new Barrier(invocationCount);

        var tasks = Enumerable.Range(0, invocationCount).Select(i => Task.Run(async () =>
        {
            using var scope = AIInvocationScope.Begin();
            var myContext = scope.Context;

            barrier.SignalAndWait();

            // Each invocation increments its own counter 10 times.
            for (var j = 0; j < 10; j++)
            {
                await Task.Yield();
                myContext.NextReferenceIndex();
            }

            // The counter should be exactly 10, not contaminated by other invocations.
            var finalIndex = myContext.NextReferenceIndex();
            Assert.Equal(11, finalIndex);
        })).ToArray();

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ToolReferences_AreIsolatedPerInvocation()
    {
        // Simulates two invocations that each add references to their ToolReferences.
        var cancellationToken = TestContext.Current.CancellationToken;

        var taskA = Task.Run(async () =>
        {
            using var scope = AIInvocationScope.Begin();
            scope.Context.ToolReferences["[doc:1]"] = new AICompletionReference
            {
                Text = "Source A",
                Index = 1,
            };

            await Task.Yield();

            // Should only see our own reference.
            Assert.Single(AIInvocationScope.Current.ToolReferences);
            Assert.Equal("Source A", AIInvocationScope.Current.ToolReferences["[doc:1]"].Text);
        }, cancellationToken);

        var taskB = Task.Run(async () =>
        {
            using var scope = AIInvocationScope.Begin();
            scope.Context.ToolReferences["[doc:1]"] = new AICompletionReference
            {
                Text = "Source B",
                Index = 1,
            };

            await Task.Yield();

            // Should only see our own reference — not Source A.
            Assert.Single(AIInvocationScope.Current.ToolReferences);
            Assert.Equal("Source B", AIInvocationScope.Current.ToolReferences["[doc:1]"].Text);
        }, cancellationToken);

        await Task.WhenAll(taskA, taskB);
    }

    [Fact]
    public void Items_AreIsolatedPerInvocation()
    {
        using var scopeA = AIInvocationScope.Begin();
        scopeA.Context.Items["key"] = "valueA";

        // Items set in one context should not be visible in another.
        var contextB = new AIInvocationContext();
        Assert.False(contextB.Items.ContainsKey("key"));
    }
}
