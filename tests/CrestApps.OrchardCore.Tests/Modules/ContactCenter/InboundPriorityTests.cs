using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Priority came only from the entry point or the queue default, so the contact the call was already matched to
/// had no bearing on where they landed in line. A tenant could mark an account as VIP, watch the CRM resolve it
/// on every inbound call, and still have that caller queue behind everybody else.
/// </summary>
public sealed class InboundPriorityTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WithNoContributors_ThePriorityIsUnchanged()
    {
        // Arrange
        // A tenant that has configured nothing must see exactly the behaviour it had before this existed.
        var resolver = new InboundPriorityResolver([]);

        // Act
        var priority = await resolver.ResolveAsync(Context(InteractionPriority.Normal), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.Normal, priority);
    }

    [Fact]
    public async Task AContributorCanRaiseThePriority()
    {
        // Arrange
        var resolver = new InboundPriorityResolver([new FixedContributor(InteractionPriority.Highest)]);

        // Act
        var priority = await resolver.ResolveAsync(Context(InteractionPriority.Normal), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.Highest, priority);
    }

    [Fact]
    public async Task TheHighestContributionWins()
    {
        // Arrange
        // Contributors describe independent reasons a caller matters — VIP, a returning callback, a repeat call
        // today. The strongest reason is the caller's priority; averaging or last-write-wins would let a weak
        // reason cancel a strong one.
        var resolver = new InboundPriorityResolver(
        [
            new FixedContributor(InteractionPriority.High),
            new FixedContributor(InteractionPriority.Highest),
            new FixedContributor(InteractionPriority.Low),
        ]);

        // Act
        var priority = await resolver.ResolveAsync(Context(InteractionPriority.Normal), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.Highest, priority);
    }

    [Fact]
    public async Task AContributorNeverLowersTheEntryPointPriority()
    {
        // Arrange
        // The entry point is an explicit operator decision about this number. A contributor exists to notice
        // something about the caller, not to overrule what the tenant configured.
        var resolver = new InboundPriorityResolver([new FixedContributor(InteractionPriority.Low)]);

        // Act
        var priority = await resolver.ResolveAsync(Context(InteractionPriority.High), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.High, priority);
    }

    [Fact]
    public async Task AContributorThatDeclinesToDecide_ChangesNothing()
    {
        // Arrange
        var resolver = new InboundPriorityResolver([new FixedContributor(null)]);

        // Act
        var priority = await resolver.ResolveAsync(Context(InteractionPriority.Normal), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.Normal, priority);
    }

    [Fact]
    public async Task AContributorThatThrows_DoesNotStopTheCallFromBeingQueued()
    {
        // Arrange
        // A contributor reads the CRM. A caller must not be dropped because a lookup failed: landing at the
        // configured priority is a worse outcome than the VIP treatment they were owed, and a far better one
        // than not reaching anybody.
        var resolver = new InboundPriorityResolver([new ThrowingContributor(), new FixedContributor(InteractionPriority.High)]);

        // Act
        var priority = await resolver.ResolveAsync(Context(InteractionPriority.Normal), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.High, priority);
    }

    [Fact]
    public async Task RepeatCaller_IsRaised_WhenTheyCalledWithinTheWindow()
    {
        // Arrange
        // Somebody calling back the same afternoon did not get what they needed the first time, and making them
        // queue from scratch is how a small problem becomes a complaint.
        var contributor = new RepeatCallerPriorityContributor(new StubClock(_now));
        var context = Context(InteractionPriority.Normal);
        context.LastInboundUtc = _now.AddHours(-2);

        // Act
        var contribution = await contributor.ContributeAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionPriority.High, contribution);
    }

    [Fact]
    public async Task RepeatCaller_IsNotRaised_ForACallLongAgo()
    {
        // Arrange
        var contributor = new RepeatCallerPriorityContributor(new StubClock(_now));
        var context = Context(InteractionPriority.Normal);
        context.LastInboundUtc = _now.AddDays(-3);

        // Act
        var contribution = await contributor.ContributeAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(contribution);
    }

    [Fact]
    public async Task RepeatCaller_IsNotRaised_ForAFirstTimeCaller()
    {
        // Arrange
        var contributor = new RepeatCallerPriorityContributor(new StubClock(_now));

        // Act
        var contribution = await contributor.ContributeAsync(Context(InteractionPriority.Normal), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(contribution);
    }

    private static InboundPriorityContext Context(InteractionPriority configured)
        => new()
        {
            ConfiguredPriority = configured,
            CustomerAddress = "+16502530000",
        };

    private sealed class FixedContributor : IInboundPriorityContributor
    {
        private readonly InteractionPriority? _contribution;

        public FixedContributor(InteractionPriority? contribution)
        {
            _contribution = contribution;
        }

        public int Order => 0;

        public Task<InteractionPriority?> ContributeAsync(InboundPriorityContext context, CancellationToken cancellationToken = default)
            => Task.FromResult(_contribution);
    }

    private sealed class ThrowingContributor : IInboundPriorityContributor
    {
        public int Order => 0;

        public Task<InteractionPriority?> ContributeAsync(InboundPriorityContext context, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The CRM lookup failed.");
    }

}
