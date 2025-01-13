using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.OpenAI.Core.Handlers;

public sealed class OpenAIChatProfileHandler : OpenAIChatProfileHandlerBase
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOpenAIChatProfileStore _profileStore;
    private readonly IOpenAIDeploymentStore _modelDeploymentStore;
    private readonly ILiquidTemplateManager _liquidTemplateManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public OpenAIChatProfileHandler(
        IHttpContextAccessor httpContextAccessor,
        IOpenAIChatProfileStore profileStore,
        IOpenAIDeploymentStore modelDeploymentStore,
        ILiquidTemplateManager liquidTemplateManager,
        IClock clock,
        IStringLocalizer<OpenAIChatProfileHandler> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _profileStore = profileStore;
        _modelDeploymentStore = modelDeploymentStore;
        _liquidTemplateManager = liquidTemplateManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingOpenAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data, true);

    public override Task UpdatingAsync(UpdatingOpenAIChatProfileContext context)
        => PopulateAsync(context.Profile, context.Data, false);

    public override async Task ValidatingAsync(ValidatingOpenAIChatProfileContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Profile.Name))
        {
            context.Result.Fail(new ValidationResult(S["Profile Name is required."], [nameof(OpenAIChatProfile.Name)]));
        }
        else
        {
            var profile = await _profileStore.FindByNameAsync(context.Profile.Name);

            if (profile is not null && profile.Id != context.Profile.Id)
            {
                context.Result.Fail(new ValidationResult(S["A profile with this name already exists. The name must be unique."], [nameof(OpenAIChatProfile.Name)]));
            }
        }

        if (string.IsNullOrWhiteSpace(context.Profile.Source))
        {
            context.Result.Fail(new ValidationResult(S["Source is required."], [nameof(OpenAIChatProfile.Source)]));
        }

        if (string.IsNullOrWhiteSpace(context.Profile.DeploymentId))
        {
            context.Result.Fail(new ValidationResult(S["DeploymentId is required."], [nameof(OpenAIChatProfile.DeploymentId)]));
        }
        else if (await _modelDeploymentStore.FindByIdAsync(context.Profile.DeploymentId) is null)
        {
            context.Result.Fail(new ValidationResult(S["Invalid DeploymentId provided."], [nameof(OpenAIChatProfile.DeploymentId)]));
        }

        if (context.Profile.Type == OpenAIChatProfileType.TemplatePrompt)
        {
            if (string.IsNullOrWhiteSpace(context.Profile.PromptTemplate))
            {
                context.Result.Fail(new ValidationResult(S["Prompt template is required."], [nameof(OpenAIChatProfile.PromptTemplate)]));
            }
            else if (!_liquidTemplateManager.Validate(context.Profile.PromptTemplate, out var _))
            {
                context.Result.Fail(new ValidationResult(S["Invalid liquid template used for Prompt template."], [nameof(OpenAIChatProfile.PromptTemplate)]));
            }
        }
    }

    public override Task InitializedAsync(InitializedOpenAIChatProfileContext context)
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

    private static Task PopulateAsync(OpenAIChatProfile profile, JsonNode data, bool isNew)
    {
        if (isNew)
        {
            var name = data[nameof(OpenAIChatProfile.Name)]?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(name))
            {
                profile.Name = name;
            }
        }

        var type = data[nameof(OpenAIChatProfile.Type)]?.GetEnumValue<OpenAIChatProfileType>();

        if (type.HasValue)
        {
            profile.Type = type.Value;
        }

        var titleType = data[nameof(OpenAIChatProfile.TitleType)]?.GetEnumValue<OpenAISessionTitleType>();

        if (titleType.HasValue)
        {
            profile.TitleType = titleType.Value;
        }

        var deploymentId = data[nameof(OpenAIChatProfile.DeploymentId)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(deploymentId))
        {
            profile.DeploymentId = deploymentId;
        }

        var systemMessage = data[nameof(OpenAIChatProfile.SystemMessage)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(systemMessage))
        {
            profile.SystemMessage = systemMessage;
        }

        var welcomeMessage = data[nameof(OpenAIChatProfile.WelcomeMessage)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(welcomeMessage))
        {
            profile.WelcomeMessage = welcomeMessage;
        }

        var functionNames = data[nameof(OpenAIChatProfile.FunctionNames)]?.AsArray();

        if (functionNames != null)
        {
            profile.FunctionNames = functionNames.Select(x => x.GetValue<string>()).ToArray();
        }

        var promptTemplate = data[nameof(OpenAIChatProfile.PromptTemplate)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(promptTemplate))
        {
            profile.PromptTemplate = promptTemplate;
        }

        var metadataNode = data["Properties"]?[nameof(OpenAIChatProfileMetadata)]?.AsObject();

        if (metadataNode == null || metadataNode.Count == 0)
        {
            return Task.CompletedTask;
        }

        var metadata = profile.As<OpenAIChatProfileMetadata>();

        var temperature = metadataNode[nameof(metadata.Temperature)]?.GetValue<float?>();

        if (temperature.HasValue)
        {
            metadata.Temperature = temperature;
        }

        var topP = metadataNode[nameof(metadata.TopP)]?.GetValue<float?>();

        if (topP.HasValue)
        {
            metadata.TopP = topP;
        }

        var frequencyPenalty = metadataNode[nameof(metadata.FrequencyPenalty)]?.GetValue<float?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.FrequencyPenalty = frequencyPenalty;
        }

        var presencePenalty = metadataNode[nameof(metadata.PresencePenalty)]?.GetValue<float?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.PresencePenalty = presencePenalty;
        }

        var maxTokens = metadataNode[nameof(metadata.MaxTokens)]?.GetValue<int?>();

        if (frequencyPenalty.HasValue)
        {
            metadata.MaxTokens = maxTokens;
        }

        var pastMessagesCount = metadataNode[nameof(metadata.PastMessagesCount)]?.GetValue<int?>();

        if (pastMessagesCount.HasValue)
        {
            metadata.PastMessagesCount = pastMessagesCount;
        }

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
