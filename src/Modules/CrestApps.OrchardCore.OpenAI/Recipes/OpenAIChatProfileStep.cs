using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.OpenAI.Recipes;

public sealed class OpenAIChatProfileStep : NamedRecipeStepHandler
{
    private readonly IOpenAIChatProfileManager _profileManager;

    internal readonly IStringLocalizer S;

    public OpenAIChatProfileStep(
        IOpenAIChatProfileManager profileManager,
        IStringLocalizer<OpenAIChatProfileStep> stringLocalizer)
        : base("OpenAIChatProfile")
    {
        _profileManager = profileManager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<OpenAIProfileStepModel>();
        var tokens = model.Profiles.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            OpenAIChatProfile profile = null;

            var id = token[nameof(OpenAIChatProfile.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                profile = await _profileManager.FindByIdAsync(id);
            }

            if (profile is null)
            {
                var name = token[nameof(OpenAIChatProfile.Name)]?.GetValue<string>()?.Trim();

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
                var sourceName = token[nameof(OpenAIChatProfile.Source)]?.GetValue<string>();

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

    private sealed class OpenAIProfileStepModel
    {
        public JsonArray Profiles { get; set; }
    }
}

