using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Programs.Models;
using CrestApps.OrchardCore.Learning.Programs.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Learning.Programs.Recipes;

/// <summary>
/// Creates or updates required learning rules, matched by id or by name. Named people come by user id.
/// </summary>
internal sealed class LearningRequirementsRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRequiredLearningService _requiredLearning;

    internal readonly IStringLocalizer S;

    public LearningRequirementsRecipeStep(IRequiredLearningService requiredLearning, IStringLocalizer<LearningRequirementsRecipeStep> stringLocalizer)
        : base(ProgramsConstants.RecipeStepName)
    {
        _requiredLearning = requiredLearning;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Requirements"] is not JsonArray rows)
        {
            return;
        }

        var existing = await _requiredLearning.ListAsync();

        foreach (var row in rows.OfType<JsonObject>())
        {
            LearningRequirement incoming;

            try
            {
                incoming = row.Deserialize<LearningRequirement>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["A required learning rule could not be read: {0}", exception.Message]);

                continue;
            }

            if (incoming is null)
            {
                continue;
            }

            var match = existing.FirstOrDefault(rule => !string.IsNullOrEmpty(incoming.ItemId) && rule.ItemId == incoming.ItemId)
                ?? existing.FirstOrDefault(rule => string.Equals(rule.Name, incoming.Name, StringComparison.OrdinalIgnoreCase));

            // Copy onto the instance the session tracks; saving the deserialized copy would store a second document.
            var rule = match ?? incoming;

            if (match is not null)
            {
                match.Name = incoming.Name;
                match.Enabled = incoming.Enabled;
                match.TargetKind = incoming.TargetKind;
                match.TargetId = incoming.TargetId;
                match.AudienceName = incoming.AudienceName;
                match.UserIds = incoming.UserIds;
                match.DueRule = incoming.DueRule;
                match.DueDate = incoming.DueDate;
                match.DueDays = incoming.DueDays;
                match.ReminderDays = incoming.ReminderDays;
                match.EscalateAfterDays = incoming.EscalateAfterDays;
            }
            else
            {
                incoming.LastRunUtc = null;
                incoming.LastRunCovered = 0;
            }

            foreach (var error in await _requiredLearning.SaveAsync(rule))
            {
                context.Errors.Add(S["The required learning rule '{0}' was refused: {1}", rule.Name ?? string.Empty, error]);
            }
        }
    }
}
