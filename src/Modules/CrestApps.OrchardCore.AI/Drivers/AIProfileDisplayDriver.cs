using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Orchestration;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProfileDisplayDriver : DisplayDriver<AIProfile>
{
    private readonly IAIProfileStore _profileStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AIOptions _aiOptions;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly OrchestratorOptions _orchestratorOptions;

    internal readonly IStringLocalizer S;

    public AIProfileDisplayDriver(
        IAIProfileStore profileStore,
        ILiquidTemplateManager liquidTemplateManager,
        IOptions<AIOptions> aiOptions,
        DefaultAIOptions defaultAIOptions,
        IOptions<OrchestratorOptions> orchestratorOptions,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<AIProfileDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _liquidTemplateManager = liquidTemplateManager;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        _aiOptions = aiOptions.Value;
        _defaultAIOptions = defaultAIOptions;
        _orchestratorOptions = orchestratorOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfile profile, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIProfile_Fields_SummaryAdmin", profile).Location("Content:1"),
            View("AIProfile_Buttons_SummaryAdmin", profile).Location("Actions:5"),
            View("AIProfile_DefaultTags_SummaryAdmin", profile).Location("Tags:5"),
            View("AIProfile_DefaultMeta_SummaryAdmin", profile).Location("Meta:5"),
             View("AIProfile_ActionsMenu_SummaryAdmin", profile)
            .Location("ActionsMenu:10")
            .RenderWhen(async () => profile.GetSettings<AIProfileSettings>().IsRemovable && await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, AIPermissions.ManageAIProfiles, profile))
        );
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        void PopulateProfileFields(EditProfileViewModel model)
        {
            var metadata = profile.As<AIProfileMetadata>();
            var agentMetadata = profile.As<AgentMetadata>();
            model.PromptSubject = profile.PromptSubject;
            model.PromptTemplate = profile.PromptTemplate;
            model.WelcomeMessage = profile.WelcomeMessage;
            model.Description = profile.Description;
            model.AddInitialPrompt = !string.IsNullOrEmpty(metadata.InitialPrompt);
            model.InitialPrompt = metadata.InitialPrompt;
            model.TitleType = profile.TitleType;
            model.ProfileType = profile.Type;
            model.AgentAvailability = agentMetadata?.Availability ?? AgentAvailability.OnDemand;
            model.TitleTypes =
            [
                new SelectListItem(S["Set the first prompt as the title"], nameof(AISessionTitleType.InitialPrompt)),
                new SelectListItem(S["Generate a title based on the first prompt"], nameof(AISessionTitleType.Generated)),
            ];

            model.ProfileTypes =
            [
                new SelectListItem(S["Chat"], nameof(AIProfileType.Chat)),
                new SelectListItem(S["Utility"], nameof(AIProfileType.Utility)),
                new SelectListItem(S["Template generated prompt"], nameof(AIProfileType.TemplatePrompt)),
                new SelectListItem(S["Agent"], nameof(AIProfileType.Agent)),
            ];

            model.AvailabilityTypes =
            [
                new SelectListItem(S["On demand"], nameof(AgentAvailability.OnDemand)),
                new SelectListItem(S["Always available"], nameof(AgentAvailability.AlwaysAvailable)),
            ];
        }

        void PopulateParameters(ProfileMetadataViewModel model)
        {
            var metadata = profile.As<AIProfileMetadata>();

            model.SystemMessage = metadata.SystemMessage;
            model.FrequencyPenalty = context.IsNew ? _defaultAIOptions.FrequencyPenalty : metadata.FrequencyPenalty;
            model.PastMessagesCount = context.IsNew ? _defaultAIOptions.PastMessagesCount : metadata.PastMessagesCount;
            model.PresencePenalty = context.IsNew ? _defaultAIOptions.PresencePenalty : metadata.PresencePenalty;
            model.Temperature = context.IsNew ? _defaultAIOptions.Temperature : metadata.Temperature;
            model.MaxTokens = context.IsNew ? _defaultAIOptions.MaxOutputTokens : metadata.MaxTokens;
            model.TopP = context.IsNew ? _defaultAIOptions.TopP : metadata.TopP;
            model.UseCaching = metadata.UseCaching;
            model.AllowCaching = _defaultAIOptions.EnableDistributedCaching;

            model.IsSystemMessageLocked = profile.GetSettings<AIProfileSettings>().LockSystemMessage;
        }

        var mainFieldsResult = Initialize<EditProfileMainFieldsViewModel>("AIProfileMainFields_Edit", model =>
        {
            model.Name = profile.Name;
            model.DisplayText = profile.DisplayText;
            model.IsNew = context.IsNew;
        }).Location("Content:1%General;1");

        var connectionFieldResult = Initialize<EditConnectionProfileViewModel>("AIProfileConnection_Edit", model =>
        {
            var orchestrators = _orchestratorOptions.GetOrchestratorDescriptors();
            if (orchestrators.Count > 1)
            {
                model.OrchestratorName = profile.OrchestratorName;
                model.Orchestrators = orchestrators
                    .Select(x => new SelectListItem(x.Value.Title ?? x.Key, x.Key))
                    .ToArray();
            }
        }).Location("Content:2%General;1");

        var generalFieldsResult = Initialize<EditProfileViewModel>("AIProfileFields_Edit", PopulateProfileFields)
            .Location("Content:5%General;1");

        var interactionFieldsResult = Initialize<EditProfileViewModel>("AIProfileInteractionFields_Edit", PopulateProfileFields)
            .Location("Content:1%Interactions;3");

        var instructionFieldsResult = Initialize<EditProfileViewModel>("AIProfileInstructionFields_Edit", PopulateProfileFields)
            .Location("Content:5%Instructions;4");

        var systemInstructionsResult = Initialize<ProfileMetadataViewModel>("AIProfileSystemInstructions_Edit", PopulateParameters)
            .Location("Content:10%Instructions;4");

        var parametersResult = Initialize<ProfileMetadataViewModel>("AIProfileParameters_Edit", PopulateParameters)
            .Location("Content:1%Parameters;5");

        return Combine(
            mainFieldsResult,
            connectionFieldResult,
            generalFieldsResult,
            interactionFieldsResult,
            instructionFieldsResult,
            systemInstructionsResult,
            parametersResult);
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProfile profile, UpdateEditorContext context)
    {
        var mainFieldsModel = new EditProfileMainFieldsViewModel();

        var model = new EditProfileViewModel();

        var connectionModel = new EditConnectionProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        await context.Updater.TryUpdateModelAsync(mainFieldsModel, Prefix);
        await context.Updater.TryUpdateModelAsync(connectionModel, Prefix);

        if (context.IsNew)
        {
            // Set the name only during profile creation. Editing the name afterward is not allowed.
            var name = mainFieldsModel.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(mainFieldsModel.Name), S["Technical name is required."]);
            }
            else if (await _profileStore.FindByNameAsync(name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(mainFieldsModel.Name), S["A profile with this name already exists. The name must be unique."]);
            }

            profile.Name = name;
        }

        if (string.IsNullOrEmpty(mainFieldsModel.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(mainFieldsModel.DisplayText), S["Title is required."]);
        }

        if (model.ProfileType == AIProfileType.TemplatePrompt)
        {
            if (string.IsNullOrEmpty(model.PromptTemplate))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["Prompt template is required."]);
            }
            else if (!_liquidTemplateManager.Validate(model.PromptTemplate, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["Invalid liquid template used for Prompt template."]);
            }
        }

        if (model.ProfileType == AIProfileType.Agent)
        {
            if (string.IsNullOrEmpty(model.Description?.Trim()))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Description), S["Description is required for agent profiles."]);
            }

            var agentMetadata = profile.As<AgentMetadata>() ?? new AgentMetadata();
            agentMetadata.Availability = model.AgentAvailability;
            profile.Put(agentMetadata);
        }

        profile.DisplayText = mainFieldsModel.DisplayText;
        profile.PromptSubject = model.PromptSubject?.Trim();
        profile.PromptTemplate = model.PromptTemplate;
        profile.WelcomeMessage = model.WelcomeMessage;
        profile.Description = model.Description?.Trim();
        profile.TitleType = model.TitleType;
        profile.Type = model.ProfileType;
        profile.OrchestratorName = connectionModel.OrchestratorName;

        var parametersModel = new ProfileMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(parametersModel, Prefix);

        var metadata = profile.As<AIProfileMetadata>();
        metadata.InitialPrompt = model.AddInitialPrompt ? model.InitialPrompt?.Trim() : null;

        metadata.FrequencyPenalty = parametersModel.FrequencyPenalty;

        if (model.ProfileType == AIProfileType.Chat && model.AddInitialPrompt && string.IsNullOrWhiteSpace(metadata.InitialPrompt))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.InitialPrompt), S["Initial prompt is required when add initial prompt is enabled."]);
        }

        if (model.ProfileType == AIProfileType.Chat)
        {
            if (!parametersModel.PastMessagesCount.HasValue)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(parametersModel.PastMessagesCount), S["Past messages count is required."]);
            }
            else if (parametersModel.PastMessagesCount.Value < 1)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(parametersModel.PastMessagesCount), S["Past messages count cannot be less than {0}.", 1]);
            }
            else
            {
                metadata.PastMessagesCount = parametersModel.PastMessagesCount.Value;
            }
        }

        metadata.PresencePenalty = parametersModel.PresencePenalty;
        metadata.Temperature = parametersModel.Temperature;
        metadata.MaxTokens = parametersModel.MaxTokens;
        metadata.TopP = parametersModel.TopP;

        if (_defaultAIOptions.EnableDistributedCaching)
        {
            metadata.UseCaching = parametersModel.UseCaching;
        }

        var settings = profile.GetSettings<AIProfileSettings>();

        if (!settings.LockSystemMessage)
        {
            metadata.SystemMessage = parametersModel.SystemMessage;
        }

        profile.Put(metadata);

        return Edit(profile, context);
    }
}
