using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIProfileHandler : CatalogEntryHandlerBase<AIProfile>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly INamedCatalog<AIProfile> _profilesCatalog;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        INamedCatalog<AIProfile> profilesCatalog,
        ILiquidTemplateManager liquidTemplateManager,
        IClock clock,
        IStringLocalizer<AIProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profilesCatalog = profilesCatalog;
        _liquidTemplateManager = liquidTemplateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data, true);

    public override Task UpdatingAsync(UpdatingContext<AIProfile> context)
        => PopulateAsync(context.Model, context.Data, false);

    public override async Task ValidatingAsync(ValidatingContext<AIProfile> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.Name))
        {
            context.Result.Fail(new ValidationResult(S["Profile Name is required."], [nameof(AIProfile.Name)]));
        }
        else
        {
            var profile = await _profilesCatalog.FindByNameAsync(context.Model.Name);

            if (profile is not null && profile.ItemId != context.Model.ItemId)
            {
                context.Result.Fail(new ValidationResult(S["A profile with this name already exists. The name must be unique."], [nameof(AIProfile.Name)]));
            }
        }

        if (context.Model.Type == AIProfileType.TemplatePrompt)
        {
            if (string.IsNullOrWhiteSpace(context.Model.PromptTemplate))
            {
                context.Result.Fail(new ValidationResult(S["Prompt template is required."], [nameof(AIProfile.PromptTemplate)]));
            }
            else if (!_liquidTemplateManager.Validate(context.Model.PromptTemplate, out var _))
            {
                context.Result.Fail(new ValidationResult(S["Invalid liquid template used for Prompt template."], [nameof(AIProfile.PromptTemplate)]));
            }
        }

        if (context.Model.Type == AIProfileType.Agent)
        {
            if (string.IsNullOrWhiteSpace(context.Model.Description))
            {
                context.Result.Fail(new ValidationResult(S["Description is required for agent profiles."], [nameof(AIProfile.Description)]));
            }
        }
    }

    public override Task InitializedAsync(InitializedContext<AIProfile> context)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Model.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Model.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    public override Task CreatingAsync(CreatingContext<AIProfile> context)
    {
        if (string.IsNullOrWhiteSpace(context.Model.DisplayText))
        {
            context.Model.DisplayText = context.Model.Name;
        }

        return Task.CompletedTask;
    }

    private static Task PopulateAsync(AIProfile profile, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var name = data[nameof(AIProfile.Name)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                profile.Name = name;
            }
        }

        var displayText = data[nameof(AIProfile.DisplayText)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(displayText))
        {
            profile.DisplayText = displayText;
        }

        var description = data[nameof(AIProfile.Description)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(description))
        {
            profile.Description = description;
        }

        var type = data[nameof(AIProfile.Type)]?.GetEnumValue<AIProfileType>();

        if (type.HasValue)
        {
            profile.Type = type.Value;
        }

        var titleType = data[nameof(AIProfile.TitleType)]?.GetEnumValue<AISessionTitleType>();

        if (titleType.HasValue)
        {
            profile.TitleType = titleType.Value;
        }

        var deploymentId = data[nameof(AIProfile.ChatDeploymentId)]?.GetValue<string>()?.Trim()
            ?? data["DeploymentId"]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.ChatDeploymentId = deploymentId;
        }

        var welcomeMessage = data[nameof(AIProfile.WelcomeMessage)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(welcomeMessage))
        {
            profile.WelcomeMessage = welcomeMessage;
        }

        var promptTemplate = data[nameof(AIProfile.PromptTemplate)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(promptTemplate))
        {
            profile.PromptTemplate = promptTemplate;
        }

        var properties = data[nameof(AIProfile.Properties)]?.AsObject();

        if (properties != null)
        {
            profile.Properties ??= [];

            var existingPropertiesSnapshot = profile.Properties.Clone();

            profile.Properties.Merge(properties, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
            });

            AIPropertiesMergeHelper.MergeNamedEntries(profile.Properties, existingPropertiesSnapshot);
        }

        var settings = data[nameof(AIProfile.Settings)]?.AsObject();

        if (settings != null)
        {
            var existingSettingsSnapshot = profile.Settings.Clone();

            profile.Settings.Merge(settings, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace,
            });

            AIPropertiesMergeHelper.MergeNamedEntries(profile.Settings, existingSettingsSnapshot);
        }

        if (string.IsNullOrWhiteSpace(profile.DisplayText))
        {
            profile.DisplayText = profile.Name;
        }

        return Task.CompletedTask;
    }
}
