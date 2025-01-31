using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIDeploymentsService
{
    private readonly ILogger _logger;

    public AzureOpenAIDeploymentsService(ILogger<AzureOpenAIDeploymentsService> logger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<CognitiveServicesAccountDeploymentResource>> GetAllAsync(AIConnectionEntry entry)
    {
        var client = new ArmClient(entry.GetCredential());

        var subscriptionId = entry.GetSubscriptionId();
        var resourceGroupName = entry.GetResourceGroupName();
        var accountName = entry.GetAccountName();

        // Get the Cognitive Services resource group.
        var cognitiveServicesAccountResourceId = CognitiveServicesAccountResource.CreateResourceIdentifier(subscriptionId, resourceGroupName, accountName);
        var cognitiveServicesAccount = client.GetCognitiveServicesAccountResource(cognitiveServicesAccountResourceId);

        // Get the collection of this CognitiveServicesAccountDeploymentResource.
        var collection = cognitiveServicesAccount.GetCognitiveServicesAccountDeployments();

        var deployments = new List<CognitiveServicesAccountDeploymentResource>();

        try
        {
            // Invoke the operation and iterate over the result.
            await foreach (var item in collection.GetAllAsync())
            {
                deployments.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve the deployments from the Cognitive API.");
        }

        return deployments;
    }
}
