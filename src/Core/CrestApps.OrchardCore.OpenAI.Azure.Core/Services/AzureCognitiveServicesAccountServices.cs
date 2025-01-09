using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.Resources;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureCognitiveServicesAccountServices
{
    private readonly ILogger _logger;

    public AzureCognitiveServicesAccountServices(ILogger<AzureCognitiveServicesAccountServices> logger)
    {
        _logger = logger;
    }

    public async Task<CognitiveServicesAccountData> GetAsync(OpenAIConnectionEntry entry)
    {
        try
        {
            var subscriptionId = entry.GetSubscriptionId();
            var resourceGroupName = entry.GetResourceGroupName();
            var accountName = entry.GetAccountName();

            var resourceGroupResourceId = ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName);

            var client = new ArmClient(entry.GetCredential());
            var resourceGroupResource = client.GetResourceGroupResource(resourceGroupResourceId);

            // get the collection of this CognitiveServicesAccountResource
            var collection = resourceGroupResource.GetCognitiveServicesAccounts();

            // invoke the operation
            var response = await collection.GetIfExistsAsync(accountName);

            var result = response.HasValue ? response.Value.Data : null;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get Azure account info from the Cognitive API.");
        }

        return null;
    }
}
