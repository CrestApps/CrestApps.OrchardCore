using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.CognitiveServices.Models;
using Azure.ResourceManager.Resources;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIModelService
{
    private readonly AzureCognitiveServicesAccountServices _azureCognitiveServicesAccountServices;
    private readonly ILogger _logger;

    public AzureOpenAIModelService(
        AzureCognitiveServicesAccountServices azureCognitiveServicesAccountServices,
        ILogger<AzureOpenAIModelService> logger)
    {
        _azureCognitiveServicesAccountServices = azureCognitiveServicesAccountServices;
        _logger = logger;
    }

    public async Task<IEnumerable<CognitiveServicesModel>> GetAsync(AIConnectionEntry connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        try
        {
            var accountInfo = await _azureCognitiveServicesAccountServices.GetAsync(connection);

            if (accountInfo == null)
            {
                _logger.LogWarning("Account info not found. Unable to get list of AI models.");

                return [];
            }

            var subscriptionId = connection.GetSubscriptionId();
            var subscriptionResourceId = SubscriptionResource.CreateResourceIdentifier(subscriptionId);

            var client = new ArmClient(connection.GetCredential());
            var subscriptionResource = client.GetSubscriptionResource(subscriptionResourceId);

            var location = new AzureLocation(accountInfo.Location);

            var items = new List<CognitiveServicesModel>();

            await foreach (var item in subscriptionResource.GetModelsAsync(location))
            {
                items.Add(item);
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get a list of AI-models from Azure OpenAI API.");
        }

        return [];
    }
}
