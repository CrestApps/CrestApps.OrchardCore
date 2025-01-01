using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class AzureModelDeploymentDisplayDriver : DisplayDriver<OpenAIDeployment>
{
    public override IDisplayResult Edit(OpenAIDeployment deployment, BuildEditorContext context)
    {
        if (deployment.Source != AzureOpenAIConstants.AzureDeploymentSourceName)
        {
            return null;
        }

        // TODO, add UI for creating OpenAI Deployment using Azure.

        return null;
    }

    public override Task<IDisplayResult> UpdateAsync(OpenAIDeployment deployment, UpdateEditorContext context)
    {
        if (deployment.Source != AzureOpenAIConstants.AzureDeploymentSourceName)
        {
            return null;
        }

        return EditAsync(deployment, context);
    }
}
