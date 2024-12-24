using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class AzureAIChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly AzureOpenAIDeploymentsService _azureOpenAIDeploymentsService;

    public AzureAIChatProfileDisplayDriver(AzureOpenAIDeploymentsService azureOpenAIDeploymentsService)
    {
        _azureOpenAIDeploymentsService = azureOpenAIDeploymentsService;
    }

    public override IDisplayResult Edit(AIChatProfile model, BuildEditorContext context)
    {
        if (model.Source is null || !model.Source.StartsWith("AzureAI"))
        {
            return null;
        }

        return Initialize<AzureAIChatProfileViewModel>("AzureAIChatProfile_Edit", async m =>
        {
            var metadata = model.As<AzureAIChatProfileMetadata>();

            var azureDeployments = await _azureOpenAIDeploymentsService.GetAsync();

            m.DeploymentName = metadata.DeploymentName;
            m.SystemMessage = metadata.SystemMessage;
            m.Strictness = metadata.Strictness;
            m.FrequencyPenalty = metadata.FrequencyPenalty;
            m.PastMessagesCount = metadata.PastMessagesCount;
            m.PresencePenalty = metadata.PresencePenalty;
            m.Temperature = metadata.Temperature;
            m.TokenLength = metadata.TokenLength;
            m.TopNDocuments = m.TopNDocuments;
            m.TopP = m.TopP;
            m.Deployments = azureDeployments.Select(x => new SelectListItem(x, x));

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile model, UpdateEditorContext context)
    {
        var viewModel = new AzureAIChatProfileViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        model.Put(new AzureAIChatProfileMetadata
        {
            DeploymentName = viewModel.DeploymentName,
            SystemMessage = viewModel.SystemMessage,
            Strictness = viewModel.Strictness,
            FrequencyPenalty = viewModel.FrequencyPenalty,
            PastMessagesCount = viewModel.PastMessagesCount,
            PresencePenalty = viewModel.PresencePenalty,
            Temperature = viewModel.Temperature,
            TokenLength = viewModel.TokenLength,
            TopNDocuments = viewModel.TopNDocuments,
            TopP = viewModel.TopP,
        });

        return Edit(model, context);
    }
}
