using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony;

public sealed class TransferTargetPolicyTests
{
    [Fact]
    public async Task DefaultPolicy_WhenTheTargetIsAnOrdinaryNumber_AllowsItUnchanged()
    {
        var policy = new DefaultTransferTargetPolicy(DialDestinationPolicyFactory.Create());

        var decision = await policy.ResolveAsync("+15551234567", CreatePrincipal(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal("+15551234567", decision.ResolvedTarget);
    }

    [Fact]
    public async Task DefaultPolicy_WhenTheTargetIsAProviderDirectoryAddress_AllowsItUnchanged()
    {
        // A provider directory entry is not an E.164 number. The default policy must not refuse it, or the
        // directory the soft phone offers stops working on a telephony-only tenant.
        var policy = new DefaultTransferTargetPolicy(DialDestinationPolicyFactory.Create());

        var decision = await policy.ResolveAsync("PJSIP/1001", CreatePrincipal(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal("PJSIP/1001", decision.ResolvedTarget);
    }

    [Theory]
    [InlineData("911")]
    [InlineData("+19005551234")]
    public async Task DefaultPolicy_WhenTheTargetIsRefusedByTheDestinationPolicy_Denies(string target)
    {
        var policy = new DefaultTransferTargetPolicy(DialDestinationPolicyFactory.Create());

        var decision = await policy.ResolveAsync(target, CreatePrincipal(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsAllowed);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Fact]
    public async Task ContactCenterPolicy_WhenTheTargetIsARawNumber_Denies()
    {
        // With Contact Center Voice enabled a transfer must go to a curated destination, never to whatever an
        // agent types into the field.
        var resolver = new Mock<ITransferDestinationResolver>();

        resolver
            .Setup(service => service.ResolveAsync(It.IsAny<TransferRequest>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferDestinationResolutionResult.Denied());

        var policy = new ContactCenterTransferTargetPolicy(resolver.Object);

        var decision = await policy.ResolveAsync("+15551234567", CreatePrincipal(), TestContext.Current.CancellationToken);

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task ContactCenterPolicy_WhenTheTargetIsACatalogEntry_ResolvesItToTheStoredAddress()
    {
        var resolver = new Mock<ITransferDestinationResolver>();

        resolver
            .Setup(service => service.ResolveAsync(
                It.Is<TransferRequest>(request => request.TargetType == InteractionTransferTargetType.External && request.TargetId == "entry-1"),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferDestinationResolutionResult.Success(InteractionTransferTargetType.External, "+15559998888"));

        resolver
            .Setup(service => service.ResolveAsync(
                It.Is<TransferRequest>(request => request.TargetType != InteractionTransferTargetType.External),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferDestinationResolutionResult.Denied());

        var policy = new ContactCenterTransferTargetPolicy(resolver.Object);

        var decision = await policy.ResolveAsync("entry-1", CreatePrincipal(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal("+15559998888", decision.ResolvedTarget);
    }

    [Fact]
    public async Task ContactCenterPolicy_WhenTheTargetIsAQueue_ResolvesIt()
    {
        var resolver = new Mock<ITransferDestinationResolver>();

        resolver
            .Setup(service => service.ResolveAsync(
                It.Is<TransferRequest>(request => request.TargetType == InteractionTransferTargetType.Queue && request.TargetId == "queue-1"),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferDestinationResolutionResult.Success(InteractionTransferTargetType.Queue, "queue-1"));

        resolver
            .Setup(service => service.ResolveAsync(
                It.Is<TransferRequest>(request => request.TargetType != InteractionTransferTargetType.Queue),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferDestinationResolutionResult.Denied());

        var policy = new ContactCenterTransferTargetPolicy(resolver.Object);

        var decision = await policy.ResolveAsync("queue-1", CreatePrincipal(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal("queue-1", decision.ResolvedTarget);
    }

    private static ClaimsPrincipal CreatePrincipal()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));
}
