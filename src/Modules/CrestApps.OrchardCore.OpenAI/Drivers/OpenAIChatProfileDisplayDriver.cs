using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Functions;
using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Drivers;

public sealed class OpenAIChatProfileDisplayDriver : DisplayDriver<OpenAIChatProfile>
{
    private readonly IOpenAIChatProfileStore _profileStore;
    private readonly IOpenAIDeploymentStore _modelDeploymentStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IEnumerable<IOpenAIChatFunction> _functions;

    internal readonly IStringLocalizer S;

    public OpenAIChatProfileDisplayDriver(
        IOpenAIChatProfileStore profileStore,
        IOpenAIDeploymentStore modelDeploymentStore,
        ILiquidTemplateManager liquidTemplateManager,
        IEnumerable<IOpenAIChatFunction> functions,
        IStringLocalizer<OpenAIChatProfileDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _modelDeploymentStore = modelDeploymentStore;
        _liquidTemplateManager = liquidTemplateManager;
        _functions = functions;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OpenAIChatProfile model, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OpenAIChatProfile_Fields_SummaryAdmin", model).Location("Content:1"),
            View("OpenAIChatProfile_Buttons_SummaryAdmin", model).Location("Actions:5"),
            View("OpenAIChatProfile_DefaultTags_SummaryAdmin", model).Location("Tags:5"),
            View("OpenAIChatProfile_DefaultMeta_SummaryAdmin", model).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(OpenAIChatProfile model, BuildEditorContext context)
    {
        var fields = Initialize<EditChatProfileViewModel>("OpenAIChatProfileFields_Edit", async m =>
        {
            m.Name = model.Name;
            m.SystemMessage = model.SystemMessage;
            m.PromptTemplate = model.PromptTemplate;
            m.WelcomeMessage = model.WelcomeMessage;
            m.DeploymentId = model.DeploymentId;
            m.TitleType = model.TitleType;
            m.IsNew = context.IsNew;

            m.ProfileType = model.Type;
            m.TitleTypes =
            [
                new SelectListItem(S["Set the first prompt as the title"], nameof(OpenAISessionTitleType.InitialPrompt)),
                new SelectListItem(S["Generate a title based on the first prompt"], nameof(OpenAISessionTitleType.Generated)),
            ];

            m.ProfileTypes =
            [
                new SelectListItem(S["Chat"], nameof(OpenAIChatProfileType.Chat)),
                new SelectListItem(S["Tool"], nameof(OpenAIChatProfileType.Tool)),
                new SelectListItem(S["Generated Prompt"], nameof(OpenAIChatProfileType.GeneratedPrompt)),
            ];

            m.Functions = _functions.OrderBy(x => x.Name).Select(x => new FunctionEntry
            {
                Name = x.Name,
                Description = x.Description,
                IsSelected = model.FunctionNames?.Contains(x.Name) ?? false,
            }).ToArray();

            m.Deployments = [];

            var deployments = (await _modelDeploymentStore.GetAllAsync())
            .GroupBy(x => x.ConnectionName)
            .Select(x => new
            {
                GroupName = x.Key,
                Items = x.OrderBy(x => x.Name),
            });

            foreach (var deployment in deployments)
            {
                var group = new SelectListGroup
                {
                    Name = deployment.GroupName,
                };

                foreach (var item in deployment.Items)
                {
                    var option = new SelectListItem
                    {
                        Text = item.Name,
                        Value = item.Id,
                        Group = group,
                    };

                    m.Deployments.Add(option);
                }
            }

        }).Location("Content:1");


        var metadata = Initialize<ChatProfileMetadataViewModel>("OpenAIChatProfileMetadata_Edit", async m =>
        {
            var metadata = model.As<OpenAIChatProfileMetadata>();

            m.FrequencyPenalty = metadata.FrequencyPenalty;
            m.PastMessagesCount = metadata.PastMessagesCount;
            m.PresencePenalty = metadata.PresencePenalty;
            m.Temperature = metadata.Temperature;
            m.MaxTokens = metadata.MaxTokens;
            m.TopP = m.TopP;

            var azureDeployments = await _modelDeploymentStore.GetAllAsync();

            m.Deployments = azureDeployments.Select(x => new SelectListItem(x.Name, x.Id));

        }).Location("Content:5");


        return Combine(fields, metadata);
    }

    public override async Task<IDisplayResult> UpdateAsync(OpenAIChatProfile model, UpdateEditorContext context)
    {
        await UpdateFieldsAsync(model, context);

        await UpdateMetadataAsync(model, context);

        return Edit(model, context);
    }

    private async Task UpdateFieldsAsync(OpenAIChatProfile model, UpdateEditorContext context)
    {
        var viewModel = new EditChatProfileViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        if (context.IsNew)
        {
            // Set the name only during profile creation. Editing the name afterward is not allowed.
            var name = viewModel.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["Name is required."]);
            }
            else if (await _profileStore.FindByNameAsync(name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.Name), S["A profile with this name already exists. The name must be unique."]);
            }

            model.Name = name;
        }

        if (string.IsNullOrEmpty(viewModel.DeploymentId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.DeploymentId), S["Deployment is required."]);
        }
        else if (await _modelDeploymentStore.FindByIdAsync(viewModel.DeploymentId) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.DeploymentId), S["Invalid deployment provided."]);
        }

        if (viewModel.ProfileType == OpenAIChatProfileType.GeneratedPrompt)
        {
            if (string.IsNullOrEmpty(viewModel.PromptTemplate))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.PromptTemplate), S["Prompt template is required."]);
            }
            else if (!_liquidTemplateManager.Validate(viewModel.PromptTemplate, out var errors))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.PromptTemplate), S["Invalid liquid template used for Prompt template."]);
            }
        }

        model.SystemMessage = viewModel.SystemMessage;
        model.PromptTemplate = viewModel.PromptTemplate;
        model.DeploymentId = viewModel.DeploymentId;
        model.WelcomeMessage = viewModel.WelcomeMessage;
        model.TitleType = viewModel.TitleType;
        model.Type = viewModel.ProfileType;

        var validFunctionNames = _functions.Select(x => x.Name).ToArray();

        model.FunctionNames = viewModel.Functions.Where(x => x.IsSelected && validFunctionNames.Contains(x.Name)).Select(x => x.Name).ToArray();
    }

    private async Task UpdateMetadataAsync(OpenAIChatProfile model, UpdateEditorContext context)
    {
        var metadataViewModel = new ChatProfileMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(metadataViewModel, Prefix);

        model.Put(new OpenAIChatProfileMetadata
        {
            FrequencyPenalty = metadataViewModel.FrequencyPenalty,
            PastMessagesCount = metadataViewModel.PastMessagesCount,
            PresencePenalty = metadataViewModel.PresencePenalty,
            Temperature = metadataViewModel.Temperature,
            MaxTokens = metadataViewModel.MaxTokens,
            TopP = metadataViewModel.TopP,
        });
    }
}
