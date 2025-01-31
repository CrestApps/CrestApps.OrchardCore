using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIChatProfileHandler : AIChatProfileHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAIChatProfileStore _profileStore;
    private readonly IAIDeploymentStore _modelDeploymentStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AIChatProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        IAIChatProfileStore profileStore,
        IAIDeploymentStore modelDeploymentStore,
        ILiquidTemplateManager liquidTemplateManager,
        IClock clock,
        IStringLocalizer<AIChatProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileStore = profileStore;
        _modelDeploymentStore = modelDeploymentStore;
        _liquidTemplateManager = liquidTemplateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data, true);

    public override Task UpdatingAsync(UpdatingAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data, false);

    public override async Task ValidatingAsync(ValidatingAIChatProfileContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Profile.Name))
        {
            context.Result.Fail(new ValidationResult(S["Profile Name is required."], [nameof(AIChatProfile.Name)]));
        }
        else
        {
            var profile = await _profileStore.FindByNameAsync(context.Profile.Name);

            if (profile is not null && profile.Id != context.Profile.Id)
            {
                context.Result.Fail(new ValidationResult(S["A profile with this name already exists. The name must be unique."], [nameof(AIChatProfile.Name)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Profile.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(AIChatProfile.Source)]));
        }

        if (string.IsNullOrWhiteSpace(context.Profile.DeploymentId))
        {
            context.Result.Fail(new ValidationResult(S["DeploymentId is required."], [nameof(AIChatProfile.DeploymentId)]));
        }
        else if (await _modelDeploymentStore.FindByIdAsync(context.Profile.DeploymentId) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid DeploymentId provided."], [nameof(AIChatProfile.DeploymentId)]));
        }

        if (context.Profile.Type == AIChatProfileType.TemplatePrompt)
        {
            if (string.IsNullOrWhiteSpace(context.Profile.PromptTemplate))
            {
                context.Result.Fail(new ValidationResult(S["Prompt template is required."], [nameof(AIChatProfile.PromptTemplate)]));
            }
            else if (!_liquidTemplateManager.Validate(context.Profile.PromptTemplate, out var _))
            {
                context.Result.Fail(new ValidationResult(S["Invalid liquid template used for Prompt template."], [nameof(AIChatProfile.PromptTemplate)]));
            }
        }
    }

    public override Task InitializedAsync(InitializedAIChatProfileContext context)
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

    private static Task PopulateAsync(AIChatProfile profile, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var name = data[nameof(AIChatProfile.Name)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                profile.Name = name;
            }
        }

        var type = data[nameof(AIChatProfile.Type)]?.GetEnumValue<AIChatProfileType>();

        if (type.HasValue)
        {
            profile.Type = type.Value;
        }

        var titleType = data[nameof(AIChatProfile.TitleType)]?.GetEnumValue<AISessionTitleType>();

        if (titleType.HasValue)
        {
            profile.TitleType = titleType.Value;
        }

        var deploymentId = data[nameof(AIChatProfile.DeploymentId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.DeploymentId = deploymentId;
        }

        var settings = profile.GetSettings<AIChatProfileSettings>();

        if (!settings.LockSystemMessage)
        {
            var systemMessage = data[nameof(AIChatProfile.SystemMessage)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(systemMessage))
            {
                profile.SystemMessage = systemMessage;
            }
        }

        var welcomeMessage = data[nameof(AIChatProfile.WelcomeMessage)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(welcomeMessage))
        {
            profile.WelcomeMessage = welcomeMessage;
        }

        var functionNames = data[nameof(AIChatProfile.FunctionNames)]?.AsArray();

        if (functionNames != null)
        {
            profile.FunctionNames = functionNames.Select(x => x.GetValue<string>()).ToArray();
        }

        var promptTemplate = data[nameof(AIChatProfile.PromptTemplate)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(promptTemplate))
        {
            profile.PromptTemplate = promptTemplate;
        }

        return Task.CompletedTask;
    }
}
