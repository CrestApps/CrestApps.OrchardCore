using CrestApps.Core;
using CrestApps.Core.AI.Documents.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Documents.AI;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.AIConfiguration;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.AI.Documents;

/// <summary>
/// The profile documents tool checks the caller's permission and lists a profile's documents, as the profile documents
/// API does.
/// </summary>
public sealed class ListAIProfileDocumentsToolTests
{
    private readonly InMemoryProfileManager _profiles = new();

    [Fact]
    public async Task List_RefusesACallerWhoIsNotSignedIn()
    {
        // Arrange
        var services = BuildServices(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProfileDocumentsTool(), services, new() { ["profileId"] = "support" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Fact]
    public async Task List_RefusesACallerWithoutManageAIProfiles()
    {
        // Arrange
        var services = BuildServices(authorize: (permission, _) => permission.Name != AIPermissions.ManageAIProfiles.Name);
        AddProfile();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProfileDocumentsTool(), services, new() { ["profileId"] = "support" });

        // Assert
        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Fact]
    public async Task List_ReturnsTheProfileDocuments()
    {
        // Arrange
        var services = BuildServices();
        AddProfile();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListAIProfileDocumentsTool(), services, new() { ["profileName"] = "support" }));

        // Assert
        var document = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("doc-1", document.GetProperty("id").GetString());
        Assert.Equal("returns-policy.pdf", document.GetProperty("fileName").GetString());
        Assert.Equal(2048, document.GetProperty("size").GetInt64());
        Assert.Equal(5, json.GetProperty("retrieval").GetProperty("topN").GetInt32());
        Assert.Equal(1, json.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task List_SaysSoWhenNoProfileHasTheId()
    {
        // Arrange
        var services = BuildServices();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListAIProfileDocumentsTool(), services, new() { ["profileId"] = "missing" });

        // Assert
        Assert.Contains("No AI profile has the ID 'missing'", result);
    }

    private void AddProfile()
    {
        var profile = new AIProfile { ItemId = "support", Name = "support", DisplayText = "Support Assistant" };
        profile.Put(new DocumentsMetadata
        {
            DocumentTopN = 5,
            Documents =
            [
                new ChatDocumentInfo { DocumentId = "doc-1", FileName = "returns-policy.pdf", ContentType = "application/pdf", FileSize = 2048 },
            ],
        });

        _profiles.Add(profile);
    }

    private ServiceProvider BuildServices(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton<IAIProfileManager>(_profiles);

        return services.BuildServiceProvider();
    }
}
