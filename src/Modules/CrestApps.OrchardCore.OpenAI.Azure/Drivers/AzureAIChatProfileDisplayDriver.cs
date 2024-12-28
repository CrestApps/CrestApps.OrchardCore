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
    private readonly IModelDeploymentStore _modelDeploymentStore;

    public AzureAIChatProfileDisplayDriver(
        AzureOpenAIDeploymentsService azureOpenAIDeploymentsService,
        IModelDeploymentStore modelDeploymentStore)
    {
        _azureOpenAIDeploymentsService = azureOpenAIDeploymentsService;
        _modelDeploymentStore = modelDeploymentStore;
    }

    public override IDisplayResult Edit(AIChatProfile model, BuildEditorContext context)
    {
        if (model.Source is null || !model.Source.StartsWith("Azure"))
        {
            return null;
        }

        return Initialize<AzureAIChatProfileViewModel>("AzureAIChatProfile_Edit", async m =>
        {
            var metadata = model.As<AzureAIChatProfileMetadata>();

            var azureDeployments = await _modelDeploymentStore.GetAllAsync();

            m.SystemMessage = metadata.SystemMessage;
            m.FrequencyPenalty = metadata.FrequencyPenalty;
            m.PastMessagesCount = metadata.PastMessagesCount;
            m.PresencePenalty = metadata.PresencePenalty;
            m.Temperature = metadata.Temperature;
            m.TokenLength = metadata.TokenLength;
            m.TopP = m.TopP;
            m.Deployments = azureDeployments.Select(x => new SelectListItem(x.Name, x.Id));

        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile model, UpdateEditorContext context)
    {
        var viewModel = new AzureAIChatProfileViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        model.Put(new AzureAIChatProfileMetadata
        {
            SystemMessage = viewModel.SystemMessage,
            FrequencyPenalty = viewModel.FrequencyPenalty,
            PastMessagesCount = viewModel.PastMessagesCount,
            PresencePenalty = viewModel.PresencePenalty,
            Temperature = viewModel.Temperature,
            TokenLength = viewModel.TokenLength,
            TopP = viewModel.TopP,
        });

        return Edit(model, context);
    }
}
