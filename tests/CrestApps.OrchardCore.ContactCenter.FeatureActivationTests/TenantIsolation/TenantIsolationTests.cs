using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.TenantIsolation;

/// <summary>
/// Two-tenant adversarial tests that pin the Contact Center tenant-isolation acceptance contract.
/// </summary>
[Collection(TenantIsolationCollection.Name)]
public sealed class TenantIsolationTests
{
    private static readonly DateTime _createdUtc = new(2026, 7, 16, 20, 0, 0, DateTimeKind.Utc);
    private readonly TenantIsolationFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantIsolationTests"/> class.
    /// </summary>
    /// <param name="fixture">The two-tenant isolation fixture.</param>
    public TenantIsolationTests(TenantIsolationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TenantState_TenantBQueriesTenantAIdentifiers_ReturnsNoTenantARecords()
    {
        // Arrange
        var scenario = nameof(TenantState_TenantBQueriesTenantAIdentifiers_ReturnsNoTenantARecords);
        var tenantAState = TenantStateSeed.For(scenario, "tenant-a");
        var tenantBState = TenantStateSeed.For(scenario, "tenant-b");
        await SeedTenantStateAsync(_fixture.TenantA, tenantAState);
        await SeedTenantStateAsync(_fixture.TenantB, tenantBState);

        // Act
        var leakedState = await QueryTenantStateAsync(_fixture.TenantB, tenantAState);
        var tenantBStateBySharedIds = await QueryTenantStateAsync(_fixture.TenantB, tenantBState);

        // Assert
        Assert.Null(leakedState.InteractionByProviderCallId);
        Assert.Null(leakedState.AgentSessionByUserId);
        Assert.Null(leakedState.ProviderCommandByCommandId);
        Assert.Equal(tenantBState.Marker, tenantBStateBySharedIds.InteractionBySharedActivityId?.CustomerAddress);
        Assert.Equal(tenantBState.Marker, tenantBStateBySharedIds.AgentSessionBySharedUserId?.DisplayName);
        Assert.Equal(tenantBState.Marker, tenantBStateBySharedIds.ProviderCommandBySharedCommandId?.ProviderReference);
    }

    [Fact]
    public async Task TenantOperation_TenantAReferencesTenantBIdentifiers_DoesNotSucceed()
    {
        // Arrange
        var tenantBState = TenantStateSeed.For(
            nameof(TenantOperation_TenantAReferencesTenantBIdentifiers_DoesNotSucceed),
            "tenant-b-negative");
        await SeedTenantStateAsync(_fixture.TenantB, tenantBState);

        // Act
        var tenantAViewOfTenantBState = await QueryTenantStateAsync(_fixture.TenantA, tenantBState);

        // Assert
        Assert.Null(tenantAViewOfTenantBState.InteractionByProviderCallId);
        Assert.Null(tenantAViewOfTenantBState.AgentSessionByUserId);
        Assert.Null(tenantAViewOfTenantBState.ProviderCommandByCommandId);
    }

    [Fact(Skip = "Enabled by cc1-asterisk; see PLAN-2 Part 0 and Part 3 tenant-scoped ARI attribution.")]
    public void AsteriskAriAttribution_TwoTenants_UsesDistinctTenantNamespacesAndScopedSubscription()
    {
        // Arrange
        // AsteriskWeb posts normalized simulator events to the Contact Center webhook and is not accepted as attribution proof.
        // Attribution must come from the real per-tenant ARI Stasis application, dialplan context, endpoint namespace, and event subscription.
        var tenantA = AsteriskTenantAriNamespace.ForTenant("tenant-a");
        var tenantB = AsteriskTenantAriNamespace.ForTenant("tenant-b");

        // Act & Assert
        Assert.NotEqual(AsteriskConstants.DefaultApplicationName, tenantA.ApplicationName);
        Assert.NotEqual(AsteriskConstants.DefaultApplicationName, tenantB.ApplicationName);
        Assert.NotEqual(tenantA.ApplicationName, tenantB.ApplicationName);
        Assert.NotEqual(tenantA.ContextName, tenantB.ContextName);
        Assert.NotEqual(tenantA.EndpointNamespace, tenantB.EndpointNamespace);
        Assert.False(tenantA.SubscribeAll);
        Assert.False(tenantB.SubscribeAll);
        Assert.StartsWith("tenant-a-", tenantA.ApplicationName, StringComparison.Ordinal);
        Assert.StartsWith("tenant-b-", tenantB.ApplicationName, StringComparison.Ordinal);
        Assert.StartsWith("tenant-a-", tenantA.ContextName, StringComparison.Ordinal);
        Assert.StartsWith("tenant-b-", tenantB.ContextName, StringComparison.Ordinal);
        Assert.StartsWith("tenant-a-", tenantA.EndpointNamespace, StringComparison.Ordinal);
        Assert.StartsWith("tenant-b-", tenantB.EndpointNamespace, StringComparison.Ordinal);
    }

    [Fact(Skip = "Enabled by cc1-asterisk; see PLAN-2 Part 3 persisted ARI channel-to-tenant ownership binding.")]
    public void AsteriskAriEvent_TenantAReceivesTenantBIdentifiers_RejectsEventBeforeRoutingOrMediaActions()
    {
        // Arrange
        var tenantA = AsteriskTenantAriNamespace.ForTenant("tenant-a");
        var tenantB = AsteriskTenantAriNamespace.ForTenant("tenant-b");
        var inboundEvent = new AsteriskTenantAriEvent(
            tenantB.ApplicationName,
            tenantB.ContextName,
            $"{tenantB.EndpointNamespace}/1001",
            "channel-owned-by-tenant-b");

        // Act
        var acceptedByTenantA = tenantA.Accepts(inboundEvent);

        // Assert
        Assert.False(acceptedByTenantA);
    }

    private async Task SeedTenantStateAsync(ContactCenterTenant tenant, TenantStateSeed seed)
    {
        await _fixture.Host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var interactionManager = services.GetRequiredService<IInteractionManager>();
            var agentSessionManager = services.GetRequiredService<IAgentSessionManager>();
            var commandManager = services.GetRequiredService<IProviderCommandManager>();

            await interactionManager.CreateAsync(new Interaction
            {
                ActivityItemId = seed.SharedActivityItemId,
                AgentId = seed.SharedAgentId,
                Channel = InteractionChannel.Voice,
                CorrelationId = seed.CorrelationId,
                CreatedUtc = _createdUtc,
                CustomerAddress = seed.Marker,
                Direction = InteractionDirection.Inbound,
                ProviderInteractionId = seed.ProviderCallId,
                ProviderName = "Asterisk",
                Status = InteractionStatus.Connected,
            });

            await agentSessionManager.CreateAsync(new AgentSession
            {
                ConnectedUtc = _createdUtc,
                ConnectionIds = [seed.ConnectionId],
                CreatedUtc = _createdUtc,
                DisplayName = seed.Marker,
                IsOnline = true,
                LastHeartbeatUtc = _createdUtc,
                QueueIds = [seed.SharedQueueId],
                UserId = seed.UserId,
                UserName = seed.UserName,
            });

            await agentSessionManager.CreateAsync(new AgentSession
            {
                ConnectedUtc = _createdUtc,
                ConnectionIds = [seed.SharedConnectionId],
                CreatedUtc = _createdUtc,
                DisplayName = seed.Marker,
                IsOnline = true,
                LastHeartbeatUtc = _createdUtc,
                QueueIds = [seed.SharedQueueId],
                UserId = seed.SharedUserId,
                UserName = seed.SharedUserName,
            });

            await commandManager.CreateAsync(new ProviderCommand
            {
                ActivityItemId = seed.SharedActivityItemId,
                CommandId = seed.CommandId,
                CommandType = ProviderCommandType.Dial,
                CreatedUtc = _createdUtc,
                LeaseExpiresUtc = _createdUtc,
                NextAttemptUtc = _createdUtc,
                ProviderName = "Asterisk",
                ProviderReference = seed.Marker,
                RequestPayload = "{}",
                Status = ProviderCommandStatus.Pending,
            });

            await commandManager.CreateAsync(new ProviderCommand
            {
                ActivityItemId = seed.SharedActivityItemId,
                CommandId = seed.SharedCommandId,
                CommandType = ProviderCommandType.Dial,
                CreatedUtc = _createdUtc,
                LeaseExpiresUtc = _createdUtc,
                NextAttemptUtc = _createdUtc,
                ProviderName = "Asterisk",
                ProviderReference = seed.Marker,
                RequestPayload = "{}",
                Status = ProviderCommandStatus.Pending,
            });
        });
    }

    private async Task<TenantStateQueryResult> QueryTenantStateAsync(ContactCenterTenant tenant, TenantStateSeed seed)
    {
        return await _fixture.Host.ExecuteInTenantScopeAsync(tenant, async services =>
        {
            var interactionManager = services.GetRequiredService<IInteractionManager>();
            var agentSessionManager = services.GetRequiredService<IAgentSessionManager>();
            var commandManager = services.GetRequiredService<IProviderCommandManager>();

            return new TenantStateQueryResult(
                await interactionManager.FindByProviderInteractionIdAsync("Asterisk", seed.ProviderCallId),
                await interactionManager.FindByActivityIdAsync(seed.SharedActivityItemId),
                await agentSessionManager.FindByUserIdAsync(seed.UserId),
                await agentSessionManager.FindByUserIdAsync(seed.SharedUserId),
                await commandManager.FindByCommandIdAsync(seed.CommandId),
                await commandManager.FindByCommandIdAsync(seed.SharedCommandId));
        });
    }

    private sealed record TenantStateQueryResult(
        Interaction InteractionByProviderCallId,
        Interaction InteractionBySharedActivityId,
        AgentSession AgentSessionByUserId,
        AgentSession AgentSessionBySharedUserId,
        ProviderCommand ProviderCommandByCommandId,
        ProviderCommand ProviderCommandBySharedCommandId);

    private sealed record TenantStateSeed(
        string Marker,
        string CorrelationId,
        string ProviderCallId,
        string UserId,
        string UserName,
        string ConnectionId,
        string SharedConnectionId,
        string CommandId,
        string SharedActivityItemId,
        string SharedAgentId,
        string SharedCommandId,
        string SharedQueueId,
        string SharedUserId,
        string SharedUserName)
    {
        public static TenantStateSeed For(
            string scenarioName,
            string tenantName)
        {
            ArgumentException.ThrowIfNullOrEmpty(scenarioName);
            ArgumentException.ThrowIfNullOrEmpty(tenantName);

            var scenarioId = scenarioName.ToLowerInvariant();

            return new TenantStateSeed(
                tenantName,
                $"{scenarioId}-{tenantName}-correlation",
                $"{scenarioId}-{tenantName}-provider-call",
                $"{scenarioId}-{tenantName}-user",
                $"{scenarioId}-{tenantName}-user-name",
                $"{scenarioId}-{tenantName}-connection",
                $"{scenarioId}-{tenantName}-shared-connection",
                $"{scenarioId}-{tenantName}-command",
                $"{scenarioId}-logical-activity-001",
                $"{scenarioId}-logical-agent-001",
                $"{scenarioId}-logical-command-001",
                $"{scenarioId}-logical-queue-001",
                $"{scenarioId}-logical-user-001",
                $"{scenarioId}-logical-user");
        }
    }

    private sealed record AsteriskTenantAriEvent(
        string ApplicationName,
        string ContextName,
        string Endpoint,
        string ChannelId);

    private sealed record AsteriskTenantAriNamespace(
        string ApplicationName,
        string ContextName,
        string EndpointNamespace,
        bool SubscribeAll)
    {
        public static AsteriskTenantAriNamespace ForTenant(string tenantName)
        {
            return new AsteriskTenantAriNamespace(
                $"{tenantName}-crestapps-telephony",
                $"{tenantName}-contact-center",
                $"{tenantName}-webrtc",
                SubscribeAll: false);
        }

        public bool Accepts(AsteriskTenantAriEvent ariEvent)
        {
            return string.Equals(ApplicationName, ariEvent.ApplicationName, StringComparison.Ordinal) &&
                string.Equals(ContextName, ariEvent.ContextName, StringComparison.Ordinal) &&
                ariEvent.Endpoint.StartsWith(EndpointNamespace + "/", StringComparison.Ordinal);
        }
    }
}
