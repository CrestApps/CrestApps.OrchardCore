using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Tooling;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels;
using CrestApps.OrchardCore.ServiceLevels.AI;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tests.Agent.AIModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using ServiceLevelsAIToolsStartup = CrestApps.OrchardCore.ServiceLevels.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.ServiceLevels.AI;

/// <summary>
/// The Service Levels AI startup requires the AI feature, registers its two read tools with their texts and without
/// approval, and registers the service level policies as a read-only record kind whose projection shows the targets and
/// never the policy's <c>Properties</c>. The visibility rule extracted from the Service-level timers report keeps only the
/// records a kind's access provider returns and hides the kinds that have none.
/// </summary>
public sealed class ServiceLevelAIRegistrationTests
{
    [Theory]
    [InlineData(ListServiceLevelRisksTool.TheName)]
    [InlineData(GetServiceLevelTimersTool.TheName)]
    public void TheTool_IsRegisteredWithItsTexts_AndAsksNoApproval(string toolName)
    {
        // Arrange
        using var provider = Build();

        // Act
        var found = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools.TryGetValue(toolName, out var tool);

        // Assert
        Assert.True(found, $"The tool '{toolName}' is not registered.");
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Service Levels", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.False(tool.RequiresApproval);
    }

    [Fact]
    public void TheStartup_RequiresTheAIFeature()
    {
        // Act
        var required = typeof(ServiceLevelsAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false)
            .Cast<RequireFeaturesAttribute>()
            .Single();

        // Assert
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
    }

    [Fact]
    public void ThePolicyKind_IsReadOnly_WithTheManagePoliciesPermission()
    {
        // Arrange
        using var provider = Build();

        // Act
        var kind = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds[ServiceLevelsAIToolsStartup.PolicyKind];

        // Assert
        Assert.False(kind.CanWrite);
        Assert.Equal(ServiceLevelsPermissions.ManageServiceLevelPolicies.Name, kind.Permission.Name);
        Assert.Equal(ServiceLevelsPermissions.ManageServiceLevelPolicies.Name, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
    }

    [Fact]
    public void ThePolicyProjection_ShowsTheTargets_AndNeverTheProperties()
    {
        // Arrange
        var policy = new ServiceLevelPolicy
        {
            ItemId = "p1",
            Name = "Standard",
            Scope = "Tickets",
            WarningPercent = 75,
            Targets =
            [
                new ServiceLevelTarget { Metric = ServiceLevelMetrics.FirstResponse, Minutes = 60 },
                new ServiceLevelTarget { Metric = ServiceLevelMetrics.Resolution, Priority = "urgent", Minutes = 240 },
                new ServiceLevelTarget { Metric = ServiceLevelMetrics.NextResponse, Minutes = 0 },
            ],
        };
        policy.Properties["WorkOrderSettings"] = "CANARY-properties";

        // Act
        var json = JsonSerializer.SerializeToElement(ServiceLevelsAIToolsStartup.DescribePolicy(policy));

        // Assert
        Assert.DoesNotContain("CANARY", json.GetRawText());
        Assert.Equal(75, json.GetProperty("warningPercent").GetInt32());

        var targets = json.GetProperty("targets").EnumerateArray().ToList();

        Assert.Equal(2, targets.Count);
        Assert.Equal("urgent", targets[1].GetProperty("priority").GetString());
        Assert.Equal(240, targets[1].GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task TheVisibilityRule_KeepsWhatTheProviderReturns_AndHidesKindsWithoutOne()
    {
        // Arrange
        var providers = ServiceLevelTimerVisibility.ByKind([new OnlyTicketA()]);
        var known = new Dictionary<(string, string), bool>();
        List<ServiceLevelTimer> buffer =
        [
            new() { ItemId = "1", Subject = new RecordRef("Ticket", "a") },
            new() { ItemId = "2", Subject = new RecordRef("Ticket", "b") },
            new() { ItemId = "3", Subject = new RecordRef("WorkOrder", "a") },
        ];

        // Act
        var kept = await ServiceLevelTimerVisibility.KeepVisibleAsync(new ClaimsPrincipal(), providers, known, buffer, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("1", Assert.Single(kept).ItemId);
        Assert.Empty(buffer);
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        new ServiceLevelsAIToolsStartup(new PassThroughStringLocalizer<ServiceLevelsAIToolsStartup>()).ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private sealed class OnlyTicketA : IServiceLevelTimerAccessProvider
    {
        public string SubjectKind => "Ticket";

        public Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<string>>(subjectIds.Where(id => id == "a").ToHashSet(StringComparer.Ordinal));
    }
}
