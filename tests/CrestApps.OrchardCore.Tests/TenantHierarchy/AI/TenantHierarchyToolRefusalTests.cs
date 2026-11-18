using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.TenantHierarchy;
using CrestApps.OrchardCore.TenantHierarchy.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy.AI;

/// <summary>
/// Every tenant hierarchy tool refuses a caller who is not signed in, a caller without the permission of the admin
/// action it mirrors, a tenant that is not the one that action runs in, and any argument it does not declare.
/// </summary>
public sealed class TenantHierarchyToolRefusalTests
{
    public static TheoryData<string, string> ParentTools => new()
    {
        { ListChildTenantsTool.TheName, "ViewChildTenants" },
        { SaveChildTenantTool.TheName, "CreateChildTenants" },
        { ChangeChildTenantStateTool.TheName, "ManageChildTenants" },
        { RemoveChildTenantTool.TheName, "RemoveChildTenants" },
        { RestoreChildTenantTool.TheName, "RemoveChildTenants" },
        { RetryChildTenantSetupTool.TheName, "CreateChildTenants" },
        { DiscardChildTenantTool.TheName, "CreateChildTenants" },
        { ListChildTenantFeaturesTool.TheName, "ManageChildFeatures" },
        { SetChildTenantFeaturesTool.TheName, "ManageChildFeatures" },
        { ListChildTenantAccessGrantsTool.TheName, "ManageChildAccess" },
        { GrantChildTenantAccessTool.TheName, "ManageChildAccess" },
        { RevokeChildTenantAccessTool.TheName, "ManageChildAccess" },
    };

    public static TheoryData<string, string> PlatformTools => new()
    {
        { GetTenantHierarchyOverviewTool.TheName, "ManageTenantHierarchy" },
        { SaveParentTenantTool.TheName, "ManageTenantHierarchy" },
        { ChangeParentTenantStateTool.TheName, "ManageTenantHierarchy" },
        { UnmakeParentTenantTool.TheName, "ManageTenantHierarchy" },
        { RemoveParentTenantTool.TheName, "ManageTenantHierarchy" },
        { MoveChildTenantTool.TheName, "ManageTenantHierarchy" },
        { DetachChildTenantTool.TheName, "ManageTenantHierarchy" },
    };

    [Theory]
    [MemberData(nameof(ParentTools))]
    [MemberData(nameof(PlatformTools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Arrange
        _ = permission;
        var services = Build(toolName, signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, []);

        // Assert
        Assert.Equal(TenantHierarchyAIToolBase.NotAllowedMessage, result);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Theory]
    [MemberData(nameof(ParentTools))]
    [MemberData(nameof(PlatformTools))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(toolName, signedIn: true, granted: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, []);

        // Assert
        Assert.Equal(TenantHierarchyAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, services.GetRequiredService<FakeAuthorizationService>().Checks[0].Permission);
    }

    [Theory]
    [MemberData(nameof(ParentTools))]
    public async Task ParentTool_RefusesATenantThatIsNotAParent(string toolName, string permission)
    {
        // Arrange
        _ = permission;
        var services = AgentToolTestContext.CreateServices().AddSingleton(TestShellSettings.Ordinary("shop")).BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, []);

        // Assert
        Assert.Contains("not a parent tenant", result, StringComparison.Ordinal);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Theory]
    [MemberData(nameof(PlatformTools))]
    public async Task PlatformTool_RefusesATenantThatIsNotTheDefaultTenant(string toolName, string permission)
    {
        // Arrange
        _ = permission;
        var services = AgentToolTestContext.CreateServices().AddSingleton(TestShellSettings.Parent("firm")).BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, []);

        // Assert
        Assert.Contains("Default tenant only", result, StringComparison.Ordinal);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    [Theory]
    [InlineData(SaveChildTenantTool.TheName, "host")]
    [InlineData(SaveChildTenantTool.TheName, "requestUrlPrefix")]
    [InlineData(SaveChildTenantTool.TheName, "connectionString")]
    [InlineData(SaveChildTenantTool.TheName, "databaseProvider")]
    [InlineData(SaveChildTenantTool.TheName, "featureProfile")]
    [InlineData(SaveParentTenantTool.TheName, "connectionString")]
    [InlineData(SaveParentTenantTool.TheName, "host")]
    public async Task WriteTool_RefusesAnArgumentItDoesNotDeclare(string toolName, string argument)
    {
        // Arrange
        var services = Build(toolName, signedIn: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, new()
        {
            ["displayName"] = "Bella's Bakery",
            [argument] = "Server=db;Password=hunter2",
        });

        // Assert
        Assert.Contains($"'{argument}' is not an argument", result, StringComparison.Ordinal);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    internal static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListChildTenantsTool.TheName => new ListChildTenantsTool(),
            SaveChildTenantTool.TheName => new SaveChildTenantTool(),
            ChangeChildTenantStateTool.TheName => new ChangeChildTenantStateTool(),
            RemoveChildTenantTool.TheName => new RemoveChildTenantTool(),
            RestoreChildTenantTool.TheName => new RestoreChildTenantTool(),
            RetryChildTenantSetupTool.TheName => new RetryChildTenantSetupTool(),
            DiscardChildTenantTool.TheName => new DiscardChildTenantTool(),
            ListChildTenantFeaturesTool.TheName => new ListChildTenantFeaturesTool(),
            SetChildTenantFeaturesTool.TheName => new SetChildTenantFeaturesTool(),
            ListChildTenantAccessGrantsTool.TheName => new ListChildTenantAccessGrantsTool(),
            GrantChildTenantAccessTool.TheName => new GrantChildTenantAccessTool(),
            RevokeChildTenantAccessTool.TheName => new RevokeChildTenantAccessTool(),
            GetTenantHierarchyOverviewTool.TheName => new GetTenantHierarchyOverviewTool(),
            SaveParentTenantTool.TheName => new SaveParentTenantTool(),
            ChangeParentTenantStateTool.TheName => new ChangeParentTenantStateTool(),
            UnmakeParentTenantTool.TheName => new UnmakeParentTenantTool(),
            RemoveParentTenantTool.TheName => new RemoveParentTenantTool(),
            MoveChildTenantTool.TheName => new MoveChildTenantTool(),
            DetachChildTenantTool.TheName => new DetachChildTenantTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };

    private static ServiceProvider Build(string toolName, bool signedIn, bool granted = true)
    {
        var isPlatform = toolName is GetTenantHierarchyOverviewTool.TheName or SaveParentTenantTool.TheName or ChangeParentTenantStateTool.TheName
            or UnmakeParentTenantTool.TheName or RemoveParentTenantTool.TheName or MoveChildTenantTool.TheName or DetachChildTenantTool.TheName;
        ShellSettings settings = isPlatform ? TestShellSettings.Default() : TestShellSettings.Parent("firm");

        return AgentToolTestContext.CreateServices(signedIn, (_, _) => granted)
            .AddSingleton(settings)
            .BuildServiceProvider();
    }
}
