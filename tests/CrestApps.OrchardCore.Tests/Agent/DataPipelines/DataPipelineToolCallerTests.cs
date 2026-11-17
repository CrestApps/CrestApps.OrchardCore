using CrestApps.OrchardCore.AI.Agent.DataPipelines;
using CrestApps.OrchardCore.AI.Agent.SharedLinks;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Agent.DataPipelines;

/// <summary>
/// Every data pipeline and shared link tool acts for the person in the chat: it refuses a caller who is not signed in
/// without checking anything, and a caller who lacks the permission the matching admin page asks for, and it reads,
/// revokes and deletes nothing when it refuses.
/// </summary>
public sealed class DataPipelineToolCallerTests
{
    public static TheoryData<string, string> Tools => new()
    {
        { ListDataPipelineStepTypesTool.TheName, "ViewDataPipelines" },
        { ListDataSetsTool.TheName, "ManageDataPipelines" },
        { GetDataSetSchemaTool.TheName, "ManageDataPipelines" },
        { ListSharedLinksTool.TheName, "ManageSharedLinks" },
        { RevokeSharedLinkTool.TheName, "ManageSharedLinks" },
        { DeleteSharedLinkTool.TheName, "ManageSharedLinks" },
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName, string permission)
    {
        // Arrange
        _ = permission;

        var harness = new DataPipelineToolHarness
        {
            SignedIn = false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
        AssertNothingTouched(harness);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenTheCallerLacksThePermissionOfTheAdminPage_Refuses(string toolName, string permission)
    {
        // Arrange
        var harness = new DataPipelineToolHarness
        {
            Authorize = (_, _) => false,
        };

        using var services = harness.Build();
        var (tool, arguments) = Create(toolName);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(tool, services, arguments);

        // Assert
        var checks = services.GetRequiredService<FakeAuthorizationService>().Checks;

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Equal(permission, Assert.Single(checks).Permission);
        AssertNothingTouched(harness);
    }

    private static void AssertNothingTouched(DataPipelineToolHarness harness)
    {
        Assert.Empty(harness.SharedLinks.Revoked);
        Assert.Empty(harness.SharedLinks.Deleted);
        Assert.Empty(harness.DataSource.Users);
    }

    private static (AIFunction Tool, Dictionary<string, object> Arguments) Create(string toolName)
    {
        return toolName switch
        {
            ListDataPipelineStepTypesTool.TheName => (new ListDataPipelineStepTypesTool(), []),
            ListDataSetsTool.TheName => (new ListDataSetsTool(), []),
            GetDataSetSchemaTool.TheName => (new GetDataSetSchemaTool(), new() { ["dataSet"] = "Shop::Orders" }),
            ListSharedLinksTool.TheName => (new ListSharedLinksTool(), []),
            RevokeSharedLinkTool.TheName => (new RevokeSharedLinkTool(), new() { ["linkId"] = "link-finance" }),
            DeleteSharedLinkTool.TheName => (new DeleteSharedLinkTool(), new() { ["linkId"] = "link-finance" }),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
    }
}
