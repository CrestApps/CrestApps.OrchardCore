using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class CreateAIProfileFromTemplateStep : NamedRecipeStepHandler
{
    public const string StepKey = "CreateAIProfileFromTemplate";

    private readonly IAIProfileManager _profileManager;
    private readonly INamedCatalogManager<AIProfileTemplate> _templateManager;

    internal readonly IStringLocalizer S;

    public CreateAIProfileFromTemplateStep(
        IAIProfileManager profileManager,
        INamedCatalogManager<AIProfileTemplate> templateManager,
        IStringLocalizer<CreateAIProfileFromTemplateStep> stringLocalizer)
        : base(StepKey)
    {
        _profileManager = profileManager;
        _templateManager = templateManager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<CreateAIProfileFromTemplateStepModel>();
        var tokens = model.Profiles?.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            var templateId = token[nameof(CreateAIProfileFromTemplateModel.TemplateId)]?.GetValue<string>()?.Trim();

            if (string.IsNullOrEmpty(templateId))
            {
                context.Errors.Add(S["'{0}' is required for '{1}' recipe step.", nameof(CreateAIProfileFromTemplateModel.TemplateId), StepKey]);
                continue;
            }

            var template = await _templateManager.FindByIdAsync(templateId)
                ?? await _templateManager.FindByNameAsync(templateId);

            if (template is null)
            {
                context.Errors.Add(S["AI template '{0}' could not be found.", templateId]);
                continue;
            }

            if (template.Source != AITemplateSources.Profile)
            {
                context.Errors.Add(S["AI template '{0}' is not a profile template.", templateId]);
                continue;
            }

            var tokenOverrides = token.DeepClone() as JsonObject ?? [];
            tokenOverrides.Remove(nameof(CreateAIProfileFromTemplateModel.TemplateId));

            var profile = await GetExistingProfileAsync(tokenOverrides)
                ?? await _profileManager.NewAsync(new JsonObject());

            ApplyTemplateToProfile(profile, template);

            await _profileManager.UpdateAsync(profile, tokenOverrides);

            var id = tokenOverrides[nameof(AIProfile.ItemId)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id) && UniqueId.IsValid(id))
            {
                profile.ItemId = id;
            }

            var validationResult = await _profileManager.ValidateAsync(profile);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _profileManager.CreateAsync(profile);
        }
    }

    private async Task<AIProfile> GetExistingProfileAsync(JsonObject token)
    {
        var id = token[nameof(AIProfile.ItemId)]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(id))
        {
            var existing = await _profileManager.FindByIdAsync(id);

            if (existing is not null)
            {
                return existing;
            }
        }

        var name = token[nameof(AIProfile.Name)]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(name))
        {
            return await _profileManager.FindByNameAsync(name);
        }

        return null;
    }

    private static void ApplyTemplateToProfile(AIProfile profile, AIProfileTemplate template)
    {
        if (template.Properties != null)
        {
            foreach (var property in template.Properties)
            {
                if (string.Equals(property.Key, nameof(ProfileTemplateMetadata), StringComparison.Ordinal) ||
                    string.Equals(property.Key, nameof(SystemPromptTemplateMetadata), StringComparison.Ordinal))
                {
                    continue;
                }

                profile.Properties[property.Key] = property.Value;
                profile.Settings[property.Key] = JsonSerializer.SerializeToNode(property.Value);
            }
        }

        if (!string.IsNullOrEmpty(template.DisplayText))
        {
            profile.DisplayText = template.DisplayText;
        }

        if (!string.IsNullOrEmpty(template.Name))
        {
            profile.Name = template.Name;
        }

        var templateMetadata = template.GetOrCreate<ProfileTemplateMetadata>();

        if (templateMetadata.ProfileType.HasValue)
        {
            profile.Type = templateMetadata.ProfileType.Value;
        }

        if (!string.IsNullOrEmpty(templateMetadata.ChatDeploymentName))
        {
            profile.ChatDeploymentName = templateMetadata.ChatDeploymentName;
        }

        if (!string.IsNullOrEmpty(templateMetadata.UtilityDeploymentName))
        {
            profile.UtilityDeploymentName = templateMetadata.UtilityDeploymentName;
        }

        if (!string.IsNullOrEmpty(templateMetadata.OrchestratorName))
        {
            profile.OrchestratorName = templateMetadata.OrchestratorName;
        }

        if (templateMetadata.TitleType.HasValue)
        {
            profile.TitleType = templateMetadata.TitleType;
        }

        if (!string.IsNullOrEmpty(templateMetadata.WelcomeMessage))
        {
            profile.WelcomeMessage = templateMetadata.WelcomeMessage;
        }

        if (!string.IsNullOrEmpty(templateMetadata.PromptSubject))
        {
            profile.PromptSubject = templateMetadata.PromptSubject;
        }

        if (!string.IsNullOrEmpty(templateMetadata.PromptTemplate))
        {
            profile.PromptTemplate = templateMetadata.PromptTemplate;
        }

        var metadata = profile.GetOrCreate<AIProfileMetadata>();

        if (!string.IsNullOrEmpty(templateMetadata.SystemMessage))
        {
            metadata.SystemMessage = templateMetadata.SystemMessage;
        }

        if (templateMetadata.Temperature.HasValue)
        {
            metadata.Temperature = templateMetadata.Temperature;
        }

        if (templateMetadata.TopP.HasValue)
        {
            metadata.TopP = templateMetadata.TopP;
        }

        if (templateMetadata.FrequencyPenalty.HasValue)
        {
            metadata.FrequencyPenalty = templateMetadata.FrequencyPenalty;
        }

        if (templateMetadata.PresencePenalty.HasValue)
        {
            metadata.PresencePenalty = templateMetadata.PresencePenalty;
        }

        if (templateMetadata.MaxOutputTokens.HasValue)
        {
            metadata.MaxTokens = templateMetadata.MaxOutputTokens;
        }

        if (templateMetadata.PastMessagesCount.HasValue)
        {
            metadata.PastMessagesCount = templateMetadata.PastMessagesCount;
        }

        profile.Put(metadata);

        if (templateMetadata.ToolNames != null && templateMetadata.ToolNames.Length > 0)
        {
            var toolMetadata = profile.GetOrCreate<FunctionInvocationMetadata>();
            toolMetadata.Names = [.. templateMetadata.ToolNames];
            profile.Put(toolMetadata);
        }

        if (templateMetadata.AgentNames != null && templateMetadata.AgentNames.Length > 0)
        {
            var agentMetadata = profile.GetOrCreate<AgentInvocationMetadata>();
            agentMetadata.Names = [.. templateMetadata.AgentNames];
            profile.Put(agentMetadata);
        }

        if (!string.IsNullOrEmpty(templateMetadata.Description))
        {
            profile.Description = templateMetadata.Description;
        }

        if (templateMetadata.AgentAvailability.HasValue)
        {
            var agentMeta = profile.GetOrCreate<AgentMetadata>();
            agentMeta.Availability = templateMetadata.AgentAvailability.Value;
            profile.Put(agentMeta);
        }
    }

    private sealed class CreateAIProfileFromTemplateStepModel
    {
        public JsonArray Profiles { get; set; }
    }

    private sealed class CreateAIProfileFromTemplateModel
    {
        public string TemplateId { get; set; }
    }
}
