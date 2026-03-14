using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

/// <summary>
/// Display driver for Profile-source AI templates.
/// Manages connection, profile-specific fields, and model parameters
/// stored in <see cref="ProfileTemplateMetadata"/>.
/// </summary>
internal sealed class ProfileTemplateDisplayDriver : DisplayDriver<AIProfileTemplate>
{
    private readonly AIProviderOptions _providerOptions;
    private readonly OrchestratorOptions _orchestratorOptions;
    private readonly IChatResponseHandlerResolver _handlerResolver;

    internal readonly IStringLocalizer S;

    public ProfileTemplateDisplayDriver(
        IOptions<AIProviderOptions> providerOptions,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IChatResponseHandlerResolver handlerResolver,
        IStringLocalizer<ProfileTemplateDisplayDriver> stringLocalizer)
    {
        _providerOptions = providerOptions.Value;
        _orchestratorOptions = orchestratorOptions.Value;
        _handlerResolver = handlerResolver;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AIProfileTemplate template, BuildEditorContext context)
    {
        var metadata = template.As<ProfileTemplateMetadata>();

        var connectionResult = Initialize<AIProfileTemplateConnectionViewModel>("AIProfileTemplateConnection_Edit", model =>
        {
            model.ConnectionName = metadata.ConnectionName;
            model.OrchestratorName = metadata.OrchestratorName;
            model.InitialResponseHandlerName = metadata.InitialResponseHandlerName;

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

            var handlers = _handlerResolver.GetAll().ToList();
            model.ResponseHandlers = handlers.Count > 1
                ? handlers
                    .Select(h => new SelectListItem(h.Name, h.Name))
                    .OrderBy(x => x.Text)
                    .ToList()
                : [];
        }).Location("Content:2")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));

        var profileFieldsResult = Initialize<AIProfileTemplateProfileFieldsViewModel>("AIProfileTemplateProfileFields_Edit", model =>
        {
            model.WelcomeMessage = metadata.WelcomeMessage;
            model.PromptTemplate = metadata.PromptTemplate;
            model.PromptSubject = metadata.PromptSubject;
            model.Description = metadata.Description;
            model.ProfileType = metadata.ProfileType;
            model.AgentAvailability = metadata.AgentAvailability;
            model.TitleType = metadata.TitleType;

            model.ProfileTypes =
            [
                new SelectListItem(S["Chat"], nameof(AIProfileType.Chat)),
                new SelectListItem(S["Utility"], nameof(AIProfileType.Utility)),
                new SelectListItem(S["Template generated prompt"], nameof(AIProfileType.TemplatePrompt)),
                new SelectListItem(S["Agent"], nameof(AIProfileType.Agent)),
            ];

            model.TitleTypes =
            [
                new SelectListItem(S["Set the first prompt as the title"], nameof(AISessionTitleType.InitialPrompt)),
                new SelectListItem(S["Generate a title based on the first prompt"], nameof(AISessionTitleType.Generated)),
            ];

            model.AvailabilityTypes =
            [
                new SelectListItem(S["On demand"], nameof(AgentAvailability.OnDemand)),
                new SelectListItem(S["Always available"], nameof(AgentAvailability.AlwaysAvailable)),
            ];
        }).Location("Content:5")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));

        var parametersResult = Initialize<AIProfileTemplateParametersViewModel>("AIProfileTemplateParameters_Edit", model =>
        {
            model.SystemMessage = metadata.SystemMessage;
            model.Temperature = metadata.Temperature;
            model.TopP = metadata.TopP;
            model.FrequencyPenalty = metadata.FrequencyPenalty;
            model.PresencePenalty = metadata.PresencePenalty;
            model.MaxOutputTokens = metadata.MaxOutputTokens;
            model.PastMessagesCount = metadata.PastMessagesCount;
        }).Location("Content:10")
        .RenderWhen(() => Task.FromResult(template.Source == AITemplateSources.Profile));

        return Combine(connectionResult, profileFieldsResult, parametersResult);
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfileTemplate template, UpdateEditorContext context)
    {
        if (template.Source != AITemplateSources.Profile)
        {
            return null;
        }

        var metadata = template.As<ProfileTemplateMetadata>();

        var connectionModel = new AIProfileTemplateConnectionViewModel();
        await context.Updater.TryUpdateModelAsync(connectionModel, Prefix);

        metadata.ConnectionName = connectionModel.ConnectionName;
        metadata.OrchestratorName = connectionModel.OrchestratorName;
        metadata.InitialResponseHandlerName = connectionModel.InitialResponseHandlerName?.Trim();

        var profileFieldsModel = new AIProfileTemplateProfileFieldsViewModel();
        await context.Updater.TryUpdateModelAsync(profileFieldsModel, Prefix);

        if (!profileFieldsModel.ProfileType.HasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(profileFieldsModel.ProfileType), S["Profile type is required."]);
        }

        metadata.WelcomeMessage = profileFieldsModel.WelcomeMessage;
        metadata.PromptTemplate = profileFieldsModel.PromptTemplate;
        metadata.PromptSubject = profileFieldsModel.PromptSubject;
        metadata.Description = profileFieldsModel.Description?.Trim();
        metadata.ProfileType = profileFieldsModel.ProfileType;
        metadata.AgentAvailability = profileFieldsModel.AgentAvailability;
        metadata.TitleType = profileFieldsModel.TitleType;

        var parametersModel = new AIProfileTemplateParametersViewModel();
        await context.Updater.TryUpdateModelAsync(parametersModel, Prefix);

        metadata.SystemMessage = parametersModel.SystemMessage;
        metadata.Temperature = parametersModel.Temperature;
        metadata.TopP = parametersModel.TopP;
        metadata.FrequencyPenalty = parametersModel.FrequencyPenalty;
        metadata.PresencePenalty = parametersModel.PresencePenalty;
        metadata.MaxOutputTokens = parametersModel.MaxOutputTokens;
        metadata.PastMessagesCount = parametersModel.PastMessagesCount;

        template.Put(metadata);

        return Edit(template, context);
    }
}
