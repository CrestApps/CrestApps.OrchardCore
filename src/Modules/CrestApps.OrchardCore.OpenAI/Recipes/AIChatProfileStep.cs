using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.OpenAI.Recipes;

public sealed class AIChatProfileStep : IRecipeStepHandler
{
    private readonly IAIChatProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    public AIChatProfileStep(
        IAIChatProfileManager profileManager,
        IStringLocalizer<AIChatProfileStep> stringLocalizer)
    {
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    public async Task ExecuteAsync(RecipeExecutionContext context)
    {
        if (!string.Equals(context.Name, "AIChatProfile", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var model = context.Step.ToObject<OpenAIProfileStepModel>();
        var tokens = model.Profiles.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIChatProfile profile = null;

            var id = token[nameof(AIChatProfile.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                profile = await _profileManager.FindByIdAsync(id);
            }

            if (profile is null)
            {
                var name = token[nameof(AIChatProfile.Name)]?.GetValue<string>()?.Trim();

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
                var sourceName = token[nameof(AIChatProfile.Source)]?.GetValue<string>();

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
}

public sealed class OpenAIProfileStepModel
{
    public JsonArray Profiles { get; set; }
}

