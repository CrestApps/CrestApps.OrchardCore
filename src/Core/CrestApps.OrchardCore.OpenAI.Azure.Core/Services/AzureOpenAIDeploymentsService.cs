using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureOpenAIDeploymentsService
{
    private readonly AzureArmOptions _azureArmOptions;
    private readonly AzureCognitiveAccountOptions _azureCognitiveAccountOptions;
    private readonly ILogger _logger;

    public AzureOpenAIDeploymentsService(
        ILogger<AzureOpenAIDeploymentsService> logger,
        IOptions<AzureArmOptions> azureArmOptions,
        IOptions<AzureCognitiveAccountOptions> azureCognitiveAccountOptions)
    {
        _azureArmOptions = azureArmOptions.Value;
        _azureCognitiveAccountOptions = azureCognitiveAccountOptions.Value;
        _logger = logger;
    }

    public async Task<IEnumerable<string>> GetAsync()
    {
        var armClient = new ArmClient(_azureArmOptions.GetTokenCredential());

        // Get the Cognitive Services resource group.
        var cognitiveServicesAccountResourceId = CognitiveServicesAccountResource.CreateResourceIdentifier(_azureArmOptions.SubscriptionId, _azureCognitiveAccountOptions.ResourceGroupName, _azureCognitiveAccountOptions.AccountName);
        var cognitiveServicesAccount = armClient.GetCognitiveServicesAccountResource(cognitiveServicesAccountResourceId);

        // Get the collection of this CognitiveServicesAccountDeploymentResource.
        var collection = cognitiveServicesAccount.GetCognitiveServicesAccountDeployments();

        var names = new List<string>();

        try
        {
            // Invoke the operation and iterate over the result.
            await foreach (var item in collection.GetAllAsync())
            {
                names.Add(item.Data.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to retrieve the deployments from Azure.");
        }

        return names;
    }
}
