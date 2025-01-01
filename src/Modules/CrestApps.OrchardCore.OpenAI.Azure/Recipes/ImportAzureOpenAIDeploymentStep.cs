using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Models;
using Json.Path;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.OpenAI.Azure.Recipes;

public sealed class ImportAzureOpenAIDeploymentStep : IRecipeStepHandler
{
    private readonly IOpenAIDeploymentManager _deploymentManager;
    private readonly AzureOpenAIDeploymentsService _azureOpenAIDeploymentsService;
    private readonly OpenAIConnectionOptions _connectionOptions;

    internal readonly IStringLocalizer S;

    public ImportAzureOpenAIDeploymentStep(
        IOpenAIDeploymentManager deploymentManager,
        AzureOpenAIDeploymentsService azureOpenAIDeploymentsService,
        IOptions<OpenAIConnectionOptions> connectionOptions,
        IStringLocalizer<ImportAzureOpenAIDeploymentStep> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        _azureOpenAIDeploymentsService = azureOpenAIDeploymentsService;
        _connectionOptions = connectionOptions.Value;
        S = stringLocalizer;
    }

    public async Task ExecuteAsync(RecipeExecutionContext context)
    {
        if (!string.Equals(context.Name, "ImportAzureOpenAIDeployment", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!_connectionOptions.Connections.TryGetValue(AzureOpenAIConstants.AzureDeploymentSourceName, out var connections))
        {
            context.Errors.Add(S["There are no connections for {0}.", AzureOpenAIConstants.AzureDeploymentSourceName]);

            return;
        }

        var importableConnections = new Dictionary<string, OpenAIConnectionEntry>(StringComparer.OrdinalIgnoreCase);

        if (context.Step.TryGetPropertyValue("ConnectionNames", out var connectionName))
        {
            if (connectionName is JsonArray names)
            {
                foreach (var name in names)
                {
                    var connection = connections.FirstOrDefault(x => name.GetValue<string>().Equals(x.Name, StringComparison.OrdinalIgnoreCase));

                    if (connection == null)
                    {
                        continue;
                    }

                    importableConnections[connection.Name] = connection;
                }
            }
            else if (connectionName.TryGetValue<string>(out var name))
            {
                if (name.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    importableConnections = connections.ToDictionary(x => x.Name);
                }
                else
                {
                    var connection = connections.FirstOrDefault(x => name.Equals(x.Name, StringComparison.OrdinalIgnoreCase));

                    if (connection != null)
                    {
                        importableConnections[connection.Name] = connection;
                    }
                }
            }
        }

        if (importableConnections.Count == 0)
        {
            context.Errors.Add(S["Please provide the list of connection names from which you wish to import deployments. Alternatively, you can specify '{0}' to import deployments from all of your connections.", "all"]);

            return;
        }

        var existingDeployments = await _deploymentManager.GetAllAsync();

        foreach (var importableConnection in importableConnections.Values)
        {
            var deployments = await _azureOpenAIDeploymentsService.GetAllAsync(importableConnection);

            foreach (var deployment in deployments)
            {
                var existingDeployment = existingDeployments.FirstOrDefault(x => x.Name.Equals(deployment.Data.Name, StringComparison.OrdinalIgnoreCase));

                if (existingDeployment != null)
                {
                    continue;
                }

                existingDeployment = await _deploymentManager.NewAsync(AzureOpenAIConstants.AzureDeploymentSourceName, new JsonObject
                {
                    { nameof(OpenAIDeployment.Name), deployment.Data.Name },
                    { nameof(OpenAIDeployment.ConnectionName), importableConnection.Name },
                });

                await _deploymentManager.SaveAsync(existingDeployment);
            }
        }
    }
}
