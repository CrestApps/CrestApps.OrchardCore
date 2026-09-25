using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An extension typed into the soft phone's transfer panel on a Contact Center call is a colleague, so it is routed the
/// way picking that colleague from the directory would route it.
/// </summary>
public sealed class SoftPhoneExtensionTransferTargetResolverTests
{
    [Fact]
    public async Task ResolveAsync_AnAgentsExtension_ResolvesToThatAgent()
    {
        // Arrange
        var resolver = CreateResolver(
            new Dictionary<string, ExtensionResolution> { ["2"] = Found("2", "user-2") },
            new AgentProfile { ItemId = "agent-2", UserId = "user-2" });

        // Act
        var target = await resolver.ResolveAsync(" 2 ", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(target.Succeeded);
        Assert.Equal("agent-2", target.AgentId);
    }

    [Fact]
    public async Task ResolveAsync_AnExtensionNobodyOwns_IsRefusedWithTheExtensionNamed()
    {
        // Arrange
        var resolver = CreateResolver(new Dictionary<string, ExtensionResolution>());

        // Act
        var target = await resolver.ResolveAsync("7", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(target.Succeeded);
        Assert.Null(target.AgentId);
        Assert.Contains("7", target.Error);
    }

    [Fact]
    public async Task ResolveAsync_TheExtensionOfSomeoneWhoIsNotAnAgent_IsRefused()
    {
        // Arrange - the Contact Center routes and records a transfer only to its own agents, queues and numbers.
        var resolver = CreateResolver(new Dictionary<string, ExtensionResolution> { ["3"] = Found("3", "user-3") });

        // Act
        var target = await resolver.ResolveAsync("3", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(target.Succeeded);
        Assert.Contains("3", target.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("+17025550199")]
    [InlineData("Bea")]
    public async Task ResolveAsync_WhatIsNotAnExtension_IsRefusedWithoutALookup(string value)
    {
        // Arrange
        var extensions = new Mock<ITelephonyExtensionResolver>(MockBehavior.Strict);
        var resolver = new SoftPhoneExtensionTransferTargetResolver(
            [extensions.Object],
            new Mock<IAgentProfileManager>(MockBehavior.Strict).Object,
            new PassThroughStringLocalizer<SoftPhoneExtensionTransferTargetResolver>());

        // Act
        var target = await resolver.ResolveAsync(value, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(target.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(target.Error));
    }

    [Fact]
    public async Task ResolveAsync_WhenExtensionsAreNotAvailable_IsRefused()
    {
        // Arrange
        var resolver = new SoftPhoneExtensionTransferTargetResolver(
            [],
            new Mock<IAgentProfileManager>(MockBehavior.Strict).Object,
            new PassThroughStringLocalizer<SoftPhoneExtensionTransferTargetResolver>());

        // Act
        var target = await resolver.ResolveAsync("2", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(target.Succeeded);
    }

    private static ExtensionResolution Found(string number, string userId)
        => new() { Found = true, Number = number, UserId = userId, DisplayName = "Colleague " + number };

    private static SoftPhoneExtensionTransferTargetResolver CreateResolver(
        IReadOnlyDictionary<string, ExtensionResolution> extensions,
        params AgentProfile[] agents)
    {
        var agentManager = new Mock<IAgentProfileManager>();

        agentManager
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => agents.FirstOrDefault(agent => agent.UserId == userId));

        return new SoftPhoneExtensionTransferTargetResolver(
            [new StubTelephonyExtensionResolver(extensions)],
            agentManager.Object,
            new PassThroughStringLocalizer<SoftPhoneExtensionTransferTargetResolver>());
    }
}
