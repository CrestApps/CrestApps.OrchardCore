using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Certifications.Recipes;

/// <summary>
/// Creates or updates education credit types, matched by id or by name.
/// </summary>
internal sealed class LearningCreditTypesRecipeStep : NamedRecipeStepHandler
{
    private readonly IEducationCreditService _credits;

    internal readonly IStringLocalizer S;

    public LearningCreditTypesRecipeStep(IEducationCreditService credits, IStringLocalizer<LearningCreditTypesRecipeStep> stringLocalizer)
        : base(CertificationsConstants.CreditTypesRecipeStepName)
    {
        _credits = credits;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["CreditTypes"] is not JsonArray rows)
        {
            return;
        }

        var existing = await _credits.ListTypesAsync();

        foreach (var row in rows.OfType<JsonObject>())
        {
            EducationCreditType incoming;

            try
            {
                incoming = row.Deserialize<EducationCreditType>(LearningCertificationPathsRecipeStep.JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["An education credit type could not be read: {0}", exception.Message]);

                continue;
            }

            if (incoming is null)
            {
                continue;
            }

            var match = existing.FirstOrDefault(type => !string.IsNullOrEmpty(incoming.ItemId) && type.ItemId == incoming.ItemId)
                ?? existing.FirstOrDefault(type => string.Equals(type.Name, incoming.Name, StringComparison.OrdinalIgnoreCase));

            // Copy onto the instance the session tracks; saving the deserialized copy would store a second document.
            var type = match ?? incoming;

            if (match is not null)
            {
                match.Name = incoming.Name;
                match.Unit = incoming.Unit;
                match.Description = incoming.Description;
                match.AllowExternalLearning = incoming.AllowExternalLearning;
            }

            foreach (var error in await _credits.SaveTypeAsync(type))
            {
                context.Errors.Add(S["The education credit type '{0}' was refused: {1}", type.Name ?? string.Empty, error]);
            }
        }
    }
}
