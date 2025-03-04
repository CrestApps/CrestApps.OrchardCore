using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.Resources;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureCognitiveServicesAccountServices
{
    private readonly ILogger _logger;

    public AzureCognitiveServicesAccountServices(ILogger<AzureCognitiveServicesAccountServices> logger)
    {
        _logger = logger;
    }

    public async Task<CognitiveServicesAccountData> GetAsync(AIProviderConnectionEntry connection)
    {
        try
        {
            var subscriptionId = connection.GetSubscriptionId();
            var resourceGroupName = connection.GetResourceGroupName();
            var accountName = connection.GetAccountName();

            var resourceGroupResourceId = ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName);

            var client = new ArmClient(connection.GetCredential());
            var resourceGroupResource = client.GetResourceGroupResource(resourceGroupResourceId);

            var collection = resourceGroupResource.GetCognitiveServicesAccounts();

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
