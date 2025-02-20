using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using Json.Path;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Recipes;

internal sealed class ImportAzureOpenAIDeploymentStep : NamedRecipeStepHandler
{
    private readonly IAIDeploymentManager _deploymentManager;
    private readonly AzureOpenAIDeploymentsService _azureOpenAIDeploymentsService;
    private readonly AIProviderOptions _connectionOptions;

    internal readonly IStringLocalizer S;

    public ImportAzureOpenAIDeploymentStep(
        IAIDeploymentManager deploymentManager,
        AzureOpenAIDeploymentsService azureOpenAIDeploymentsService,
        IOptions<AIProviderOptions> connectionOptions,
        IStringLocalizer<ImportAzureOpenAIDeploymentStep> stringLocalizer)
        : base("ImportAzureOpenAIDeployment")
    {
        _deploymentManager = deploymentManager;
        _azureOpenAIDeploymentsService = azureOpenAIDeploymentsService;
        _connectionOptions = connectionOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (!_connectionOptions.Providers.TryGetValue(AzureOpenAIConstants.ProviderName, out var provider))
        {
            context.Errors.Add(S["There are no connections for {0}.", AzureOpenAIConstants.ProviderName]);

            return;
        }

        var importableConnections = new Dictionary<string, AIProviderConnection>(StringComparer.OrdinalIgnoreCase);

        if (context.Step.TryGetPropertyValue("ConnectionNames", out var connectionName))
        {
            if (connectionName is JsonArray names)
            {
                foreach (var name in names)
                {
                    var stringName = name.GetValue<string>();

                    if (!provider.Connections.TryGetValue(stringName, out var connectionProperty))
                    {
                        continue;
                    }

                    importableConnections[stringName] = connectionProperty;
                }
            }
            else if (connectionName.TryGetValue<string>(out var name))
            {
                if (name.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    importableConnections = new(provider.Connections);
                }
                else if (provider.Connections.TryGetValue(name, out var connection))
                {
                    importableConnections[name] = connection;
                }
            }
        }

        if (importableConnections.Count == 0)
        {
            context.Errors.Add(S["Please provide the list of connection names from which you wish to import deployments. Alternatively, you can specify '{0}' to import deployments from all of your connections.", "all"]);

            return;
        }

        var existingDeployments = await _deploymentManager.GetAsync(AzureOpenAIConstants.ProviderName);

        foreach (var importableConnection in importableConnections)
        {
            var deployments = await _azureOpenAIDeploymentsService.GetAllAsync(importableConnection.Value);

            foreach (var deployment in deployments)
            {
                var deploymentName = deployment.Data.Name;

                var existingDeployment = existingDeployments.FirstOrDefault(x => x.ConnectionName.Equals(importableConnection.Key, StringComparison.OrdinalIgnoreCase) && x.Name.Equals(deploymentName, StringComparison.OrdinalIgnoreCase));

                if (existingDeployment != null)
                {
                    continue;
                }

                existingDeployment = await _deploymentManager.NewAsync(AzureOpenAIConstants.ProviderName, new JsonObject
                {
                    { nameof(AIDeployment.Name), deploymentName },
                    { nameof(AIDeployment.ProviderName), AzureOpenAIConstants.ProviderName },
                    { nameof(AIDeployment.ConnectionName), importableConnection.Key },
                });

                await _deploymentManager.SaveAsync(existingDeployment);
            }
        }
    }
}
