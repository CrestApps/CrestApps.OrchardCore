using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

public sealed class AIChatProfileDisplayDriver : DisplayDriver<AIChatProfile>
{
    private readonly IAIChatProfileStore _profileStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IAIToolsService _toolsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AIProviderOptions _connectionOptions;

    internal readonly IStringLocalizer S;

    public AIChatProfileDisplayDriver(
        IAIChatProfileStore profileStore,
        ILiquidTemplateManager liquidTemplateManager,
        IAIToolsService toolsService,
        IServiceProvider serviceProvider,
        IOptions<AIProviderOptions> connectionOptions,
        IStringLocalizer<AIChatProfileDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _liquidTemplateManager = liquidTemplateManager;
        _toolsService = toolsService;
        _serviceProvider = serviceProvider;
        _connectionOptions = connectionOptions.Value;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIChatProfile profile, BuildDisplayContext context)
    {
        return CombineAsync(
            View("AIChatProfile_Fields_SummaryAdmin", profile).Location("Content:1"),
            View("AIChatProfile_Buttons_SummaryAdmin", profile).Location("Actions:5"),
            View("AIChatProfile_DefaultTags_SummaryAdmin", profile).Location("Tags:5"),
            View("AIChatProfile_DefaultMeta_SummaryAdmin", profile).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(AIChatProfile profile, BuildEditorContext context)
    {
        var mainFieldsResult = Initialize<EditChatProfileMainFieldsViewModel>("AIChatProfileMainFields_Edit", model =>
        {
            model.Name = profile.Name;
            model.DisplayText = profile.DisplayText;
            model.IsNew = context.IsNew;
        }).Location("Content:1");

        var connectionFieldResult = Initialize<EditConnectionChatProfileViewModel>("AIChatProfileConnection_Edit", model =>
        {
            var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

            if (profileSource is not null && _connectionOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider))
            {
                if (provider.Connections.Count == 1)
                {
                    // At this point, there is no deployment associated with the profile. Check the connections to obtain available deployments.

                    var connection = provider.Connections.First();
                    model.ConnectionName = connection.Key;
                }

                model.ConnectionNames = provider.Connections.Select(x => new SelectListItem(x.Key, x.Key)).ToArray();
            }
            else
            {
                model.ConnectionNames = [];
            }
        }).Location("Content:2");

        var fieldsResult = Initialize<EditChatProfileViewModel>("AIChatProfileFields_Edit", model =>
        {
            if (profile.TryGetSettings<AIChatProfileSettings>(out var settings))
            {
                model.IsOnAdminMenu = settings.IsOnAdminMenu;
            }
            else
            {
                model.IsOnAdminMenu = profile.Type == AIChatProfileType.Chat && context.IsNew;
            }

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
                new SelectListItem(S["Chat"], nameof(AIChatProfileType.Chat)),
                new SelectListItem(S["Utility"], nameof(AIChatProfileType.Utility)),
                new SelectListItem(S["Template generated prompt"], nameof(AIChatProfileType.TemplatePrompt)),
            ];

            model.Functions = _toolsService.GetFunctions()
            .OrderBy(function => function.Metadata.Name)
            .Select(function => new FunctionEntry
            {
                Name = function.Metadata.Name,
                Description = function.Metadata.Description,
                IsSelected = profile.FunctionNames?.Contains(function.Metadata.Name) ?? false,
            }).ToArray();
        }).Location("Content:5");

        return Combine(mainFieldsResult, connectionFieldResult, fieldsResult);
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile profile, UpdateEditorContext context)
    {
        var mainFieldsModel = new EditChatProfileMainFieldsViewModel();

        var model = new EditChatProfileViewModel();

        var connectionModel = new EditConnectionChatProfileViewModel();

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
            var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

            if (profileSource is not null &&
                _connectionOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider) &&
                !provider.Connections.TryGetValue(connectionModel.ConnectionName, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(connectionModel.ConnectionName), S["Invalid connection provided."]);
            }
        }

        if (model.ProfileType == AIChatProfileType.TemplatePrompt)
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

        profile.AlterSettings<AIChatProfileSettings>(settings =>
        {
            settings.IsOnAdminMenu = profile.Type == AIChatProfileType.Chat && model.IsOnAdminMenu;
        });

        var selectedFunctionNames = model.Functions?.Where(x => x.IsSelected).Select(x => x.Name).ToArray();

        if (selectedFunctionNames is null || selectedFunctionNames.Length == 0)
        {
            profile.FunctionNames = [];
        }
        else
        {
            profile.FunctionNames = _toolsService.GetFunctions()
                .Select(x => x.Metadata.Name)
                .Intersect(selectedFunctionNames)
                .ToArray();
        }

        return Edit(profile, context);
    }
}
