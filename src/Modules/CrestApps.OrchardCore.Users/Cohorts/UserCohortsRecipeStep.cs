using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Cohorts;

/// <summary>
/// Creates or updates cohorts, matched by id and then by name, and adds the people it names by user name or e-mail.
/// People already in a cohort and not named stay in it.
/// </summary>
internal sealed class UserCohortsRecipeStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The actor recorded on the people a recipe adds.
    /// </summary>
    public const string Actor = "Recipe";

    private readonly ICohortService _cohorts;
    private readonly UserManager<IUser> _userManager;

    internal readonly IStringLocalizer S;

    public UserCohortsRecipeStep(ICohortService cohorts, UserManager<IUser> userManager, IStringLocalizer<UserCohortsRecipeStep> stringLocalizer)
        : base(UsersConstants.CohortsRecipeStepName)
    {
        _cohorts = cohorts;
        _userManager = userManager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step["Cohorts"] is not JsonArray rows)
        {
            return;
        }

        foreach (var row in rows.OfType<JsonObject>())
        {
            var id = row["Id"]?.GetValue<string>()?.Trim();
            var name = row["Name"]?.GetValue<string>()?.Trim();
            var description = row["Description"]?.GetValue<string>();

            var cohort = (string.IsNullOrEmpty(id) ? null : await _cohorts.FindByIdAsync(id))
                ?? await _cohorts.FindByNameAsync(name);

            var result = cohort is null
                ? await _cohorts.CreateAsync(name, description, id)
                : await _cohorts.UpdateAsync(cohort.CohortId, name, description);

            if (!result.Succeeded)
            {
                context.Errors.Add(S["The cohort '{0}' was refused: {1}", name ?? id ?? string.Empty, string.Join(" ", result.Errors)]);

                continue;
            }

            if (row["Members"] is not JsonArray members)
            {
                continue;
            }

            var userIds = new List<string>();

            foreach (var member in members.Select(node => node?.GetValue<string>()?.Trim()).Where(value => !string.IsNullOrEmpty(value)))
            {
                var user = await _userManager.FindByNameAsync(member) ?? (member.Contains('@') ? await _userManager.FindByEmailAsync(member) : null);

                if (user is null)
                {
                    context.Errors.Add(S["The cohort '{0}' names '{1}', who is not a user.", result.Cohort.Name, member]);

                    continue;
                }

                userIds.Add(await _userManager.GetUserIdAsync(user));
            }

            await _cohorts.AddMembersAsync(result.Cohort.CohortId, userIds, Actor);
        }
    }
}

/// <summary>
/// Describes the UserCohorts recipe step.
/// </summary>
internal sealed class UserCohortsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => UsersConstants.CohortsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(UsersConstants.CohortsRecipeStepName).Description("Recipe step discriminator. Must be 'UserCohorts'.")),
                ("Cohorts", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Id", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The cohort id, kept so courses linked to the cohort find it; at most 50 characters. Empty creates a new id.")),
                        ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name, unique among cohorts without regard to case.")),
                        ("Description", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("A description.")),
                        ("Members", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("User names or e-mail addresses of people to add; people already in the cohort stay.")))
                    .Required("Name")).Description("The cohorts to create, or update by id and then by name.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }
}
