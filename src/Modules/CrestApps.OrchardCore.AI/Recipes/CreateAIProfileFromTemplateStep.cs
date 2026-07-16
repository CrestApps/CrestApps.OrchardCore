using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class CreateAIProfileFromTemplateStep : NamedRecipeStepHandler
{
    public const string StepKey = "CreateAIProfileFromTemplate";

    private readonly IAIProfileManager _profileManager;
    private readonly INamedSourceCatalogManager<AIProfileTemplate> _templateManager;

    internal readonly IStringLocalizer S;

    public CreateAIProfileFromTemplateStep(
        IAIProfileManager profileManager,
        INamedSourceCatalogManager<AIProfileTemplate> templateManager,
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
            var isNew = false;
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

            var profile = await GetExistingProfileAsync(tokenOverrides);

            if (profile is null)
            {
                isNew = true;
                profile = await _profileManager.NewAsync(new JsonObject());
            }

            AIProfileTemplateApplicator.Apply(profile, template);

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

            if (isNew)
            {
                await _profileManager.CreateAsync(profile);
            }
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

    private sealed class CreateAIProfileFromTemplateStepModel
    {
        public JsonArray Profiles { get; set; }
    }

    private sealed class CreateAIProfileFromTemplateModel
    {
        public string TemplateId { get; set; }
    }
}
