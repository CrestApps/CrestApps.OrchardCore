using CrestApps.OrchardCore.AI.Agent.ContentTypes;
using CrestApps.OrchardCore.AI.Agent.Recipes;
using CrestApps.OrchardCore.AI.Agent.System;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Agent.Recipes;

/// <summary>
/// The tools that import a recipe can change anything a recipe step can, as the JSON import of the Deployment admin
/// can, so each refuses a caller who is not signed in or does not hold that page's Import permission, before it reads
/// the recipe (review of pull request 1334, F5).
/// </summary>
public sealed class RecipeImportToolPermissionTests
{
    public static TheoryData<string> Tools => new()
    {
        ImportOrchardTool.TheName,
        ApplySystemSettingsTool.TheName,
        CreateOrUpdateWorkflowTool.TheName,
        CreateOrUpdateContentTypeDefinitionsTool.TheName,
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WithoutTheImportPermission_Refuses(string toolName)
    {
        // Arrange
        using var services = AgentToolTestContext.CreateServices(authorize: (permission, _) => permission.Name != "Import").BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, new() { ["recipe"] = "{ \"steps\": [] }" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == "Import");
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task InvokeAsync_WhenNobodyIsSignedIn_RefusesWithoutAnyCheck(string toolName)
    {
        // Arrange
        using var services = AgentToolTestContext.CreateServices(signedIn: false).BuildServiceProvider();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, new() { ["recipe"] = "{ \"steps\": [] }" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(services.GetRequiredService<FakeAuthorizationService>().Checks);
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            ImportOrchardTool.TheName => new ImportOrchardTool(),
            ApplySystemSettingsTool.TheName => new ApplySystemSettingsTool(),
            CreateOrUpdateWorkflowTool.TheName => new CreateOrUpdateWorkflowTool(),
            CreateOrUpdateContentTypeDefinitionsTool.TheName => new CreateOrUpdateContentTypeDefinitionsTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
