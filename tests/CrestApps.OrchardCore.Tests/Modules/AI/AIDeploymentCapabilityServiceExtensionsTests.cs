using CrestApps.Core;
using CrestApps.Core.AI.Capabilities;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.AI;

/// <summary>
/// Covers which deployments the chat and utility pickers offer. A deployment that cannot hold a text
/// conversation only fails at run time if an operator can pick it, but the filter has to stay lenient or
/// every deployment configured before capabilities existed would vanish from the editors.
/// </summary>
public sealed class AIDeploymentCapabilityServiceExtensionsTests
{
    [Fact]
    public void WhereCanHoldTextConversation_KeepsADeploymentThatDeclaresTextGeneration()
    {
        var deployment = CreateDeployment("chatty", AIDeploymentFeatureNames.TextGeneration);

        var kept = CreateService().WhereCanHoldTextConversation([deployment]);

        Assert.Equal(["chatty"], kept.Select(item => item.Name));
    }

    // A speech-to-speech model clears text generation, and asking it for a text completion fails.
    [Fact]
    public void WhereCanHoldTextConversation_DropsASpeechToSpeechOnlyDeployment()
    {
        var deployment = CreateDeployment("voice-only", AIDeploymentFeatureNames.Realtime);

        var kept = CreateService().WhereCanHoldTextConversation([deployment]);

        Assert.Empty(kept);
    }

    // Deployments predating the capabilities editor carry no metadata and are unconstrained at run time.
    // Filtering them out would empty every picker on an existing installation.
    [Fact]
    public void WhereCanHoldTextConversation_KeepsADeploymentThatDeclaresNothing()
    {
        var deployment = new AIDeployment { Name = "legacy" };

        var kept = CreateService().WhereCanHoldTextConversation([deployment]);

        Assert.Equal(["legacy"], kept.Select(item => item.Name));
    }

    private static AIDeployment CreateDeployment(string name, params string[] features)
    {
        var deployment = new AIDeployment { Name = name };

        deployment.Put(new AIDeploymentMetadata { Features = features });

        return deployment;
    }

    private static DefaultAIDeploymentCapabilityService CreateService()
    {
        var options = new AIDeploymentCapabilityOptions();

        foreach (var name in new[] { AIDeploymentFeatureNames.TextGeneration, AIDeploymentFeatureNames.Realtime })
        {
            options.Features[name] = new AIDeploymentFeatureDescriptor { Name = name };
        }

        return new DefaultAIDeploymentCapabilityService(Options.Create(options), null);
    }
}
