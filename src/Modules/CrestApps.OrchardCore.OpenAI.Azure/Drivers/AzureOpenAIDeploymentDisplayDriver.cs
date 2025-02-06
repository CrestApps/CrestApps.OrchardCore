using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAIDeploymentDisplayDriver : DisplayDriver<AIDeployment>
{
    public override IDisplayResult Edit(AIDeployment deployment, BuildEditorContext context)
    {
        if (deployment.ProviderName != AzureOpenAIConstants.AzureProviderName)
        {
            return null;
        }

        // TODO, add UI for creating OpenAI Deployment using Azure.

        return null;
    }

    public override Task<IDisplayResult> UpdateAsync(AIDeployment deployment, UpdateEditorContext context)
    {
        if (deployment.ProviderName != AzureOpenAIConstants.AzureProviderName)
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return EditAsync(deployment, context);
    }
}
