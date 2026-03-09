using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileTemplateDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly INamedCatalog<AIProfileTemplate> _templatesCatalog;

    internal readonly IStringLocalizer S;

    public AIProfileTemplateDisplayDriver(
        INamedCatalog<AIProfileTemplate> templatesCatalog,
        IStringLocalizer<AIProfileTemplateDisplayDriver> stringLocalizer)
    {
        _templatesCatalog = templatesCatalog;
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

        var profileFieldsResult = Initialize<AIProfileTemplateProfileFieldsViewModel>("AIProfileTemplateProfileFields_Edit", model =>
        {
            model.SystemMessage = template.SystemMessage;
            model.WelcomeMessage = template.WelcomeMessage;
            model.PromptTemplate = template.PromptTemplate;
            model.PromptSubject = template.PromptSubject;
            model.ProfileType = template.ProfileType;
            model.TitleType = template.TitleType;
            model.ConnectionName = template.ConnectionName;
            model.OrchestratorName = template.OrchestratorName;
        }).Location("Content:5");

        var parametersResult = Initialize<AIProfileTemplateParametersViewModel>("AIProfileTemplateParameters_Edit", model =>
        {
            model.Temperature = template.Temperature;
            model.TopP = template.TopP;
            model.FrequencyPenalty = template.FrequencyPenalty;
            model.PresencePenalty = template.PresencePenalty;
            model.MaxOutputTokens = template.MaxOutputTokens;
            model.PastMessagesCount = template.PastMessagesCount;
        }).Location("Content:10");

        return Combine(fieldsResult, profileFieldsResult, parametersResult);
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

        var profileFieldsModel = new AIProfileTemplateProfileFieldsViewModel();
        await context.Updater.TryUpdateModelAsync(profileFieldsModel, Prefix);

        template.SystemMessage = profileFieldsModel.SystemMessage;
        template.WelcomeMessage = profileFieldsModel.WelcomeMessage;
        template.PromptTemplate = profileFieldsModel.PromptTemplate;
        template.PromptSubject = profileFieldsModel.PromptSubject;
        template.ProfileType = profileFieldsModel.ProfileType;
        template.TitleType = profileFieldsModel.TitleType;
        template.ConnectionName = profileFieldsModel.ConnectionName;
        template.OrchestratorName = profileFieldsModel.OrchestratorName;

        var parametersModel = new AIProfileTemplateParametersViewModel();
        await context.Updater.TryUpdateModelAsync(parametersModel, Prefix);

        template.Temperature = parametersModel.Temperature;
        template.TopP = parametersModel.TopP;
        template.FrequencyPenalty = parametersModel.FrequencyPenalty;
        template.PresencePenalty = parametersModel.PresencePenalty;
        template.MaxOutputTokens = parametersModel.MaxOutputTokens;
        template.PastMessagesCount = parametersModel.PastMessagesCount;

        return Edit(template, context);
    }
}
