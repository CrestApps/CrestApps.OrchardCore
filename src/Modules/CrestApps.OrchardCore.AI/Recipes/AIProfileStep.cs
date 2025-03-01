using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIProfileStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIProfile";

    private readonly INamedModelManager<AIProfile> _profileManager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public AIProfileStep(
        INamedModelManager<AIProfile> profileManager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIProfileStep> stringLocalizer)
        : base(StepKey)
    {
        _profileManager = profileManager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIProfileStepModel>();
        var tokens = model.Profiles.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIProfile profile = null;

            var id = token[nameof(AIProfile.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                profile = await _profileManager.FindByIdAsync(id);
            }

            if (profile is null)
            {
                var name = token[nameof(AIProfile.Name)]?.GetValue<string>()?.Trim();

                if (!string.IsNullOrEmpty(name))
                {
                    profile = await _profileManager.FindByNameAsync(name);
                }
            }

            if (profile is not null)
            {
                await _profileManager.UpdateAsync(profile, token);
            }
            else
            {
                var sourceName = token[nameof(AIProfile.Source)]?.GetValue<string>();

                if (string.IsNullOrEmpty(sourceName))
                {
                    context.Errors.Add(S["Could not find profile-source value. The profile will not be imported"]);

                    continue;
                }

                if (!_aiOptions.ProfileSources.TryGetValue(sourceName, out var entry))
                {
                    context.Errors.Add(S["Unable to find a profile-source that can handle the source '{0}'.", sourceName]);

                    return;
                }

                profile = await _profileManager.NewAsync(sourceName, token);
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

            await _profileManager.SaveAsync(profile);
        }
    }

    private sealed class AIProfileStepModel
    {
        public JsonArray Profiles { get; set; }
    }
}
