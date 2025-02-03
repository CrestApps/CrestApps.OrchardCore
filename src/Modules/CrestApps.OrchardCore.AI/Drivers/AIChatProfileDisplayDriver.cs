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
    private readonly IAIDeploymentManager _modelDeploymentManager;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IAIToolsService _toolsService;
    private readonly IServiceProvider _serviceProvider;
    private readonly AIProviderOptions _connectionOptions;

    internal readonly IStringLocalizer S;

    public AIChatProfileDisplayDriver(
        IAIChatProfileStore profileStore,
        IAIDeploymentManager modelDeploymentManager,
        ILiquidTemplateManager liquidTemplateManager,
        IAIToolsService toolsService,
        IServiceProvider serviceProvider,
        IOptions<AIProviderOptions> connectionOptions,
        IStringLocalizer<AIChatProfileDisplayDriver> stringLocalizer)
    {
        _profileStore = profileStore;
        _modelDeploymentManager = modelDeploymentManager;
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
        return Initialize<EditChatProfileViewModel>("AIChatProfileFields_Edit", async model =>
        {
            if (profile.TryGetSettings<AIChatProfileSettings>(out var settings))
            {
                model.IsOnAdminMenu = settings.IsOnAdminMenu;
            }
            else
            {
                model.IsOnAdminMenu = profile.Type == AIChatProfileType.Chat && context.IsNew;
            }

            var hasDeployment = false;

            if (!string.IsNullOrEmpty(profile.DeploymentId))
            {
                var deployment = await _modelDeploymentManager.FindByIdAsync(profile.DeploymentId);

                if (deployment is not null)
                {
                    hasDeployment = true;
                    model.ConnectionName = deployment.ConnectionName;
                    model.Deployments = (await _modelDeploymentManager.GetAsync(profile.Source, deployment.ConnectionName)).Select(x => new SelectListItem(x.Name, x.Id));
                }
            }

            model.Source = profile.Source;
            model.Name = profile.Name;
            model.DisplayText = profile.DisplayText;
            model.PromptSubject = profile.PromptSubject;
            model.PromptTemplate = profile.PromptTemplate;
            model.WelcomeMessage = profile.WelcomeMessage;
            model.DeploymentId = profile.DeploymentId;
            model.TitleType = profile.TitleType;
            model.IsNew = context.IsNew;
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

            var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

            if (profileSource is not null && _connectionOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider))
            {
                if (!hasDeployment && provider.Connections.Count == 1)
                {
                    // At this point, there is no deployment associated with the profile. Check the connections to obtain available deployments.

                    var connection = provider.Connections.First();
                    model.ConnectionName = connection.Key;
                    model.Deployments = (await _modelDeploymentManager.GetAsync(profile.Source, connection.Key)).Select(x => new SelectListItem(x.Name, x.Id));
                }

                model.ConnectionNames = provider.Connections.Select(x => new SelectListItem(x.Key, x.Key)).ToArray();
            }
            else
            {
                model.ConnectionNames = [];
            }

        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIChatProfile profile, UpdateEditorContext context)
    {
        var model = new EditChatProfileViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            // Set the name only during profile creation. Editing the name afterward is not allowed.
            var name = model.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
            }
            else if (await _profileStore.FindByNameAsync(name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["A profile with this name already exists. The name must be unique."]);
            }

            profile.Name = name;
        }

        if (string.IsNullOrEmpty(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["Display Text is required."]);
        }

        var hasConnection = !string.IsNullOrEmpty(model.ConnectionName);

        if (!string.IsNullOrEmpty(model.DeploymentId))
        {
            var deployment = await _modelDeploymentManager.FindByIdAsync(model.DeploymentId);

            if (deployment is null ||
                !hasConnection ||
                !deployment.ConnectionName.Equals(model.ConnectionName, StringComparison.OrdinalIgnoreCase))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.DeploymentId), S["Invalid deployment provided."]);
            }
        }

        if (hasConnection)
        {
            var profileSource = _serviceProvider.GetKeyedService<IAIChatProfileSource>(profile.Source);

            if (profileSource is not null &&
                _connectionOptions.Providers.TryGetValue(profileSource.ProviderName, out var provider) &&
                !provider.Connections.TryGetValue(model.ConnectionName, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionName), S["Invalid connection provided."]);
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

        profile.DisplayText = model.DisplayText;
        profile.PromptSubject = model.PromptSubject?.Trim();
        profile.PromptTemplate = model.PromptTemplate;
        profile.DeploymentId = model.DeploymentId;
        profile.WelcomeMessage = model.WelcomeMessage;
        profile.TitleType = model.TitleType;
        profile.Type = model.ProfileType;

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
