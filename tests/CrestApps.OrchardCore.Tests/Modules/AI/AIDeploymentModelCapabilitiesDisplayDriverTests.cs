using System.Reflection;
using CrestApps.Core.AI.Models;

namespace CrestApps.OrchardCore.Tests.Modules.AI;

/// <summary>
/// Covers which capabilities the deployment editor shows as declared. The rule matters because saving the
/// editor always writes metadata, so what it shows for a deployment that has never declared anything is
/// what that deployment ends up constrained to.
/// </summary>
public sealed class AIDeploymentModelCapabilitiesDisplayDriverTests
{
    private static readonly AIDeploymentFeatureDescriptor[] _registeredFeatures =
    [
        new() { Name = AIDeploymentFeatureNames.TextGeneration, EnabledByDefault = true },
        new() { Name = AIDeploymentFeatureNames.ToolCalling, EnabledByDefault = true },
        new() { Name = AIDeploymentFeatureNames.Streaming, EnabledByDefault = true },
        new() { Name = AIDeploymentFeatureNames.Reasoning },
        new() { Name = AIDeploymentFeatureNames.StructuredOutputs },
    ];

    // A deployment created before capabilities existed carries no metadata and is unconstrained at
    // runtime. Showing an empty set would let an unrelated edit save it as "declares nothing", silently
    // costing it streaming and tool calling.
    [Fact]
    public void ResolveSelectedFeatures_OffersTheDefaults_WhenNoCapabilitiesWereEverDeclared()
    {
        var selected = ResolveSelectedFeatures(hasMetadata: false, metadata: null);

        Assert.Equal(
            [AIDeploymentFeatureNames.Streaming, AIDeploymentFeatureNames.TextGeneration, AIDeploymentFeatureNames.ToolCalling],
            selected.OrderBy(feature => feature, StringComparer.Ordinal));
    }

    [Fact]
    public void ResolveSelectedFeatures_KeepsWhatTheDeploymentDeclares()
    {
        var metadata = new AIDeploymentMetadata
        {
            Features = [AIDeploymentFeatureNames.Realtime],
        };

        var selected = ResolveSelectedFeatures(hasMetadata: true, metadata);

        Assert.Equal([AIDeploymentFeatureNames.Realtime], selected);
    }

    // Clearing every box is a deliberate choice, and re-ticking the defaults underneath the operator
    // would make it impossible to save a deployment that genuinely supports none of them.
    [Fact]
    public void ResolveSelectedFeatures_LeavesAnOperatorClearedListEmpty()
    {
        var metadata = new AIDeploymentMetadata
        {
            Features = [],
        };

        var selected = ResolveSelectedFeatures(hasMetadata: true, metadata);

        Assert.Empty(selected);
    }

    private static HashSet<string> ResolveSelectedFeatures(bool hasMetadata, AIDeploymentMetadata metadata)
    {
        var type = typeof(CrestApps.OrchardCore.AI.Startup).Assembly
            .GetType("CrestApps.OrchardCore.AI.Drivers.AIDeploymentModelCapabilitiesDisplayDriver", throwOnError: true)!;

        var method = type.GetMethod("ResolveSelectedFeatures", BindingFlags.NonPublic | BindingFlags.Static)!;

        return (HashSet<string>)method.Invoke(null, [hasMetadata, metadata, _registeredFeatures])!;
    }
}
