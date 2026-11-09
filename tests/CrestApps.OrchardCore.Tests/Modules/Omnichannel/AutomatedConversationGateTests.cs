using CrestApps.OrchardCore.Omnichannel.Core.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// One automated reply per conversation at a time. This used to be a process-wide static dictionary; as a
/// registered service it is per tenant, so one tenant's conversation can no longer cancel another tenant's
/// generation that happens to share a session id.
/// </summary>
public sealed class AutomatedConversationGateTests
{
    [Fact]
    public void Begin_MarksTheConversationAsGenerating_UntilTheGenerationIsDisposed()
    {
        var gate = new InMemoryAutomatedConversationGate();

        Assert.False(gate.IsGenerating("session-1"));

        using (gate.Begin("session-1", CancellationToken.None))
        {
            Assert.True(gate.IsGenerating("session-1"));
        }

        Assert.False(gate.IsGenerating("session-1"));
    }

    [Fact]
    public void Begin_CancelsTheGenerationItSupersedes()
    {
        // A newer inbound message makes the reply being composed stale, so it is cancelled and the whole turn
        // unwinds; only the newest turn sends.
        var gate = new InMemoryAutomatedConversationGate();

        using var first = gate.Begin("session-1", CancellationToken.None);

        Assert.False(first.Token.IsCancellationRequested);

        using var second = gate.Begin("session-1", CancellationToken.None);

        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(second.Token.IsCancellationRequested);
    }

    [Fact]
    public void DisposingASupersededGeneration_DoesNotReleaseTheConversation()
    {
        // The superseded turn disposes in its own finally. If that released the slot, the newer turn would look
        // like it was not generating and a recovery pass could start a third reply on top of it.
        var gate = new InMemoryAutomatedConversationGate();

        var first = gate.Begin("session-1", CancellationToken.None);
        using var second = gate.Begin("session-1", CancellationToken.None);

        first.Dispose();

        Assert.True(gate.IsGenerating("session-1"));
    }

    [Fact]
    public void Conversations_AreIndependentOfOneAnother()
    {
        var gate = new InMemoryAutomatedConversationGate();

        using var first = gate.Begin("session-1", CancellationToken.None);
        using var second = gate.Begin("session-2", CancellationToken.None);

        Assert.False(first.Token.IsCancellationRequested);
        Assert.False(second.Token.IsCancellationRequested);
        Assert.True(gate.IsGenerating("session-1"));
        Assert.True(gate.IsGenerating("session-2"));
    }

    [Fact]
    public void TwoGates_DoNotShareState()
    {
        // The point of making this a service: two tenants each get their own gate, so the same session id in
        // both cannot cancel across the boundary.
        var first = new InMemoryAutomatedConversationGate();
        var second = new InMemoryAutomatedConversationGate();

        using var generation = first.Begin("session-1", CancellationToken.None);

        Assert.True(first.IsGenerating("session-1"));
        Assert.False(second.IsGenerating("session-1"));
    }

    [Fact]
    public void TheGenerationToken_HonoursTheCallersOwnToken()
    {
        var gate = new InMemoryAutomatedConversationGate();
        using var host = new CancellationTokenSource();

        using var generation = gate.Begin("session-1", host.Token);

        host.Cancel();

        Assert.True(generation.Token.IsCancellationRequested);
    }

    [Fact]
    public void IsGenerating_WithNoSessionId_IsFalse()
    {
        var gate = new InMemoryAutomatedConversationGate();

        Assert.False(gate.IsGenerating(null));
        Assert.False(gate.IsGenerating(string.Empty));
    }
}
