using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

public sealed class AIProfileStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIProfile";

    private readonly IAIProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    public AIProfileStep(
        IAIProfileManager profileManager,
        IStringLocalizer<AIProfileStep> stringLocalizer)
        : base(StepKey)
    {
        _profileManager = profileManager;
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

                profile = await _profileManager.NewAsync(sourceName, token);

                if (profile == null)
                {
                    context.Errors.Add(S["Unable to find a profile-source that can handle the source '{Source}'.", sourceName]);

                    continue;
                }
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
