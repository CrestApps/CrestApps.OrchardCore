using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Services;
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
    private readonly INamedModelStore<AIProfile> _profileStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly AIOptions _aiOptions;
    private readonly DefaultAIOptions _defaultAIOptions;
    private readonly AIProviderOptions _connectionOptions;

    internal readonly IStringLocalizer S;

    public AIProfileDisplayDriver(
        INamedModelStore<AIProfile> profileStore,
        ILiquidTemplateManager liquidTemplateManager,
        IOptions<AIOptions> aiOptions,
        IOptions<AIProviderOptions> connectionOptions,
        IOptions<DefaultAIOptions> defaultAIOptions,
        IStringLocalizer<AIProfileDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _liquidTemplateManager = liquidTemplateManager;
        _aiOptions = aiOptions.Value;
        _defaultAIOptions = defaultAIOptions.Value;
        _connectionOptions = connectionOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProfile profile, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIProfile_Fields_SummaryAdmin", profile).Location("Content:1"),
            View("AIProfile_Buttons_SummaryAdmin", profile).Location("Actions:5"),
            View("AIProfile_DefaultTags_SummaryAdmin", profile).Location("Tags:5"),
            View("AIProfile_DefaultMeta_SummaryAdmin", profile).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIProfile profile, BuildEditorContext context)
    {
        var mainFieldsResult = Initialize<EditProfileMainFieldsViewModel>("AIProfileMainFields_Edit", model =>
        {
            model.Name = profile.Name;
            model.DisplayText = profile.DisplayText;
            model.IsNew = context.IsNew;
        }).Location("Content:1");

        var connectionFieldResult = Initialize<EditConnectionProfileViewModel>("AIProfileConnection_Edit", model =>
        {
            if (!_aiOptions.ProfileSources.TryGetValue(profile.Source, out var profileSource))
            {
                return;
            }
            if (profileSource is not null && _connectionOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider))
            {
                if (provider.Connections.Count == 1)
                {
                    // At this point, there is no deployment associated with the profile. Check the connections to obtain available deployments.

                    var connection = provider.Connections.First();
                    model.ConnectionName = connection.Key;
                }
                else
                {
                    model.ConnectionName = profile.ConnectionName;
                }

                model.ConnectionNames = provider.Connections.Select(x => new SelectListItem(x.Value.TryGetValue("ConnectionNameAlias", out var a) ? a.ToString() : x.Key, x.Key)).ToArray();
            }
            else
            {
                model.ConnectionNames = [];
            }
        }).Location("Content:2");

        var fieldsResult = Initialize<EditProfileViewModel>("AIProfileFields_Edit", model =>
        {
            model.PromptSubject = profile.PromptSubject;
            model.PromptTemplate = profile.PromptTemplate;
            model.WelcomeMessage = profile.WelcomeMessage;
            model.TitleType = profile.TitleType;
            model.ProfileType = profile.Type;
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
            ];
        }).Location("Content:5");

        var parametersResult = Initialize<ProfileMetadataViewModel>("AIProfileParameters_Edit", model =>
        {
            var metadata = profile.As<AIProfileMetadata>();

            model.SystemMessage = metadata.SystemMessage;
            model.FrequencyPenalty = metadata.FrequencyPenalty;
            model.PastMessagesCount = metadata.PastMessagesCount;
            model.PresencePenalty = metadata.PresencePenalty;
            model.Temperature = metadata.Temperature;
            model.MaxTokens = metadata.MaxTokens;
            model.TopP = metadata.TopP;
            model.UseCaching = metadata.UseCaching;
            model.AllowCaching = _defaultAIOptions.EnableDistributedCaching;

            model.IsSystemMessageLocked = profile.GetSettings<AIProfileSettings>().LockSystemMessage;
        }).Location("Content:10");

        return Combine(mainFieldsResult, connectionFieldResult, fieldsResult, parametersResult);
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
                context.Updater.ModelState.AddModelError(Prefix, nameof(mainFieldsModel.Name), S["Name is required."]);
            }
            else if (await _profileStore.FindByNameAsync(name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(mainFieldsModel.Name), S["A profile with this name already exists. The name must be unique."]);
            }

            profile.Name = name;
        }

        if (string.IsNullOrEmpty(mainFieldsModel.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(mainFieldsModel.DisplayText), S["Display Text is required."]);
        }

        if (!string.IsNullOrEmpty(connectionModel.ConnectionName))
        {
            if (_aiOptions.ProfileSources.TryGetValue(profile.Source, out var profileSource) &&
                _connectionOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider) &&
                !provider.Connections.TryGetValue(connectionModel.ConnectionName, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(connectionModel.ConnectionName), S["Invalid connection provided."]);
            }
        }

        if (model.ProfileType == AIProfileType.TemplatePrompt)
        {
            if (string.IsNullOrEmpty(model.PromptTemplate))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["Prompt template is required."]);
            }
            else if (!_liquidTemplateManager.Validate(model.PromptTemplate, out var errors))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.PromptTemplate), S["Invalid liquid template used for Prompt template."]);
            }
        }

        profile.DisplayText = mainFieldsModel.DisplayText;
        profile.PromptSubject = model.PromptSubject?.Trim();
        profile.PromptTemplate = model.PromptTemplate;
        profile.WelcomeMessage = model.WelcomeMessage;
        profile.TitleType = model.TitleType;
        profile.Type = model.ProfileType;
        profile.ConnectionName = connectionModel.ConnectionName;

        var parametersModel = new ProfileMetadataViewModel();

        await context.Updater.TryUpdateModelAsync(parametersModel, Prefix);

        var metadata = profile.As<AIProfileMetadata>();

        metadata.FrequencyPenalty = parametersModel.FrequencyPenalty;
        metadata.PastMessagesCount = parametersModel.PastMessagesCount;
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
