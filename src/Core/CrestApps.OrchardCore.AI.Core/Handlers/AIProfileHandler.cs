using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIProfileHandler : AIProfileHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAIProfileStore _profileStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        IAIProfileStore profileStore,
        ILiquidTemplateManager liquidTemplateManager,
        IClock clock,
        IStringLocalizer<AIProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileStore = profileStore;
        _liquidTemplateManager = liquidTemplateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data, true);

    public override Task UpdatingAsync(UpdatingAIProfileContext context)
        => PopulateAsync(context.Profile, context.Data, false);

    public override async Task ValidatingAsync(ValidatingAIProfileContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Profile.Name))
        {
            context.Result.Fail(new ValidationResult(S["Profile Name is required."], [nameof(AIProfile.Name)]));
        }
        else
        {
            var profile = await _profileStore.FindByNameAsync(context.Profile.Name);

            if (profile is not null && profile.Id != context.Profile.Id)
            {
                context.Result.Fail(new ValidationResult(S["A profile with this name already exists. The name must be unique."], [nameof(AIProfile.Name)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Profile.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(AIProfile.Source)]));
        }

        if (context.Profile.Type == AIProfileType.TemplatePrompt)
        {
            if (string.IsNullOrWhiteSpace(context.Profile.PromptTemplate))
            {
                context.Result.Fail(new ValidationResult(S["Prompt template is required."], [nameof(AIProfile.PromptTemplate)]));
            }
            else if (!_liquidTemplateManager.Validate(context.Profile.PromptTemplate, out var _))
            {
                context.Result.Fail(new ValidationResult(S["Invalid liquid template used for Prompt template."], [nameof(AIProfile.PromptTemplate)]));
            }
        }
    }

    public override Task InitializedAsync(InitializedAIProfileContext context)
    {
        context.Profile.CreatedUtc = _clock.UtcNow;

        var user = _httpContextAccessor.HttpContext?.User;

        if (user != null)
        {
            context.Profile.OwnerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            context.Profile.Author = user.Identity.Name;
        }

        return Task.CompletedTask;
    }

    public override Task SavingAsync(SavingAIProfileContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Profile.DisplayText))
        {
            context.Profile.DisplayText = context.Profile.Name;
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

        var deploymentId = data[nameof(AIProfile.DeploymentId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.DeploymentId = deploymentId;
        }

        var connectionName = data[nameof(AIProfile.ConnectionName)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(connectionName))
        {
            profile.ConnectionName = connectionName;
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
            profile.Properties = properties.Clone();
        }

        var settings = data[nameof(AIProfile.Settings)]?.AsObject();

        if (settings != null)
        {
            foreach (var pair in settings)
            {
                profile.Settings[pair.Key] = pair.Value.Clone();
            }
        }

        return Task.CompletedTask;
    }
}
