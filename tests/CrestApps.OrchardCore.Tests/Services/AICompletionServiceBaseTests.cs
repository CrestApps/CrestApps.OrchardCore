using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Models;

namespace CrestApps.OrchardCore.Tests.Services;

public sealed class AICompletionServiceBaseTests
{
    [Fact]
    public static void GetDefaultDeploymentName_WhenConnectionDoesNotDefineDeployment_FallsBackToProviderDefault()
    {
        var providerOptions = new AIProviderOptions();
        var service = new TestAICompletionService(providerOptions);
        var provider = new AIProvider
        {
            DefaultDeploymentName = "provider-default",
            Connections = new Dictionary<string, AIProviderConnectionEntry>(StringComparer.OrdinalIgnoreCase)
            {
                ["default"] = new(new Dictionary<string, object>
                {
                    ["Endpoint"] = "http://localhost:11434",
                }),
            },
        };

        var deploymentName = service.GetDeploymentName(provider, "default");

        Assert.Equal("provider-default", deploymentName);
    }

    private sealed class TestAICompletionService : AICompletionServiceBase
    {
        public TestAICompletionService(AIProviderOptions providerOptions)
            : base(providerOptions)
        {
        }

        public string GetDeploymentName(AIProvider provider, string connectionName)
            => GetDefaultDeploymentName(provider, connectionName);
    }
}
