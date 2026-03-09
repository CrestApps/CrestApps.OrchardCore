using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;
    private readonly AIProviderOptions _providerOptions;
    private readonly OrchestratorOptions _orchestratorOptions;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateDisplayDriver(
        INamedCatalog<AIProfileTemplate> templatesCatalog,
        IOptions<AIProviderOptions> providerOptions,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IStringLocalizer<AIProfileTemplateDisplayDriver> stringLocalizer)
    {
        _templatesCatalog = templatesCatalog;
        _providerOptions = providerOptions.Value;
        _orchestratorOptions = orchestratorOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfileTemplate template, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIProfileTemplate_Fields_SummaryAdmin", template).Location("Content:1"),
            View("AIProfileTemplate_Buttons_SummaryAdmin", template).Location("Actions:5"),
            View("AIProfileTemplate_DefaultTags_SummaryAdmin", template).Location("Tags:5"),
            View("AIProfileTemplate_DefaultMeta_SummaryAdmin", template).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        var fieldsResult = Initialize<AIProfileTemplateFieldsViewModel>("AIProfileTemplateFields_Edit", model =>
        {
            model.DisplayText = template.DisplayText;
            model.Name = template.Name;
            model.Description = template.Description;
            model.Category = template.Category;
            model.IsListable = template.IsListable;
            model.IsNew = context.IsNew;
        }).Location("Content:1");

        var connectionResult = Initialize<AIProfileTemplateConnectionViewModel>("AIProfileTemplateConnection_Edit", model =>
        {
            model.ConnectionName = template.ConnectionName;
            model.OrchestratorName = template.OrchestratorName;

            model.ConnectionNames = _providerOptions.Providers
                .SelectMany(p => p.Value.Connections)
                .Select(c => new SelectListItem(
                    c.Value.TryGetValue("ConnectionNameAlias", out var alias) ? alias.ToString() : c.Key,
                    c.Key))
                .DistinctBy(x => x.Value)
                .OrderBy(x => x.Text)
                .ToList();

            model.Orchestrators = _orchestratorOptions.GetOrchestratorDescriptors()
                .Select(x => new SelectListItem(x.Value.Title ?? x.Key, x.Key))
                .ToList();
        }).Location("Content:2");

        var profileFieldsResult = Initialize<AIProfileTemplateProfileFieldsViewModel>("AIProfileTemplateProfileFields_Edit", model =>
        {
            model.WelcomeMessage = template.WelcomeMessage;
            model.PromptTemplate = template.PromptTemplate;
            model.PromptSubject = template.PromptSubject;
            model.ProfileType = template.ProfileType;
            model.TitleType = template.TitleType;

            model.ProfileTypes =
            [
                new SelectListItem(S["Chat"], nameof(AIProfileType.Chat)),
                new SelectListItem(S["Utility"], nameof(AIProfileType.Utility)),
                new SelectListItem(S["Template generated prompt"], nameof(AIProfileType.TemplatePrompt)),
            ];

            model.TitleTypes =
            [
                new SelectListItem(S["Set the first prompt as the title"], nameof(AISessionTitleType.InitialPrompt)),
                new SelectListItem(S["Generate a title based on the first prompt"], nameof(AISessionTitleType.Generated)),
            ];
        }).Location("Content:5");

        var parametersResult = Initialize<AIProfileTemplateParametersViewModel>("AIProfileTemplateParameters_Edit", model =>
        {
            model.SystemMessage = template.SystemMessage;
            model.Temperature = template.Temperature;
            model.TopP = template.TopP;
            model.FrequencyPenalty = template.FrequencyPenalty;
            model.PresencePenalty = template.PresencePenalty;
            model.MaxOutputTokens = template.MaxOutputTokens;
            model.PastMessagesCount = template.PastMessagesCount;
        }).Location("Content:10");

        return Combine(fieldsResult, connectionResult, profileFieldsResult, parametersResult);
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        var fieldsModel = new AIProfileTemplateFieldsViewModel();
        await context.Updater.TryUpdateModelAsync(fieldsModel, Prefix);

        if (context.IsNew)
        {
            if (string.IsNullOrEmpty(fieldsModel.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(fieldsModel.Name), S["Name is required."]);
            }
            else if (await _templatesCatalog.FindByNameAsync(fieldsModel.Name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(fieldsModel.Name), S["Another profile template with the same name exists."]);
            }

            template.Name = fieldsModel.Name;
        }

        if (string.IsNullOrWhiteSpace(fieldsModel.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(fieldsModel.DisplayText), S["Title is required."]);
        }

        template.DisplayText = fieldsModel.DisplayText;
        template.Description = fieldsModel.Description;
        template.Category = fieldsModel.Category;
        template.IsListable = fieldsModel.IsListable;

        var connectionModel = new AIProfileTemplateConnectionViewModel();
        await context.Updater.TryUpdateModelAsync(connectionModel, Prefix);

        template.ConnectionName = connectionModel.ConnectionName;
        template.OrchestratorName = connectionModel.OrchestratorName;

        var profileFieldsModel = new AIProfileTemplateProfileFieldsViewModel();
        await context.Updater.TryUpdateModelAsync(profileFieldsModel, Prefix);

        if (!profileFieldsModel.ProfileType.HasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(profileFieldsModel.ProfileType), S["Profile type is required."]);
        }

        template.WelcomeMessage = profileFieldsModel.WelcomeMessage;
        template.PromptTemplate = profileFieldsModel.PromptTemplate;
        template.PromptSubject = profileFieldsModel.PromptSubject;
        template.ProfileType = profileFieldsModel.ProfileType;
        template.TitleType = profileFieldsModel.TitleType;

        var parametersModel = new AIProfileTemplateParametersViewModel();
        await context.Updater.TryUpdateModelAsync(parametersModel, Prefix);

        template.SystemMessage = parametersModel.SystemMessage;
        template.Temperature = parametersModel.Temperature;
        template.TopP = parametersModel.TopP;
        template.FrequencyPenalty = parametersModel.FrequencyPenalty;
        template.PresencePenalty = parametersModel.PresencePenalty;
        template.MaxOutputTokens = parametersModel.MaxOutputTokens;
        template.PastMessagesCount = parametersModel.PastMessagesCount;

        return Edit(template, context);
    }
}
