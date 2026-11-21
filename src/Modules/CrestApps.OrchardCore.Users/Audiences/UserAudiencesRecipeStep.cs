using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// Creates or updates the audiences administrators define, matched by name.
/// </summary>
internal sealed class UserAudiencesRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IUserAudienceService _audiences;

    internal readonly IStringLocalizer S;

    public UserAudiencesRecipeStep(IUserAudienceService audiences, IStringLocalizer<UserAudiencesRecipeStep> stringLocalizer)
        : base(UsersConstants.AudiencesRecipeStepName)
    {
        _audiences = audiences;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Audiences"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            UserAudienceDefinition definition;

            try
            {
                definition = row.Deserialize<UserAudienceDefinition>(JsonOptions);
            }
            catch (JsonException exception)
            {
                context.Errors.Add(S["An audience could not be read: {0}", exception.Message]);

                continue;
            }

            if (definition is null)
            {
                continue;
            }

            definition.Rules ??= [];

            foreach (var error in await _audiences.SaveDefinitionAsync(definition))
            {
                context.Errors.Add(S["The audience '{0}' was refused: {1}", definition.Name ?? string.Empty, error]);
            }
        }
    }
}
