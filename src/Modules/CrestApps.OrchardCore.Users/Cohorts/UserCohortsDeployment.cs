using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Users.Cohorts;

/// <summary>
/// Exports every cohort with its people, by user name.
/// </summary>
public sealed class UserCohortsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<UserCohortsDeploymentStep>("Users");
    private static readonly LocalizationSource _title = LocalizationSource.Create<UserCohortsDeploymentStep>("Cohorts");

    /// <inheritdoc/>
    public override string Name => UsersConstants.CohortsRecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class UserCohortsDeploymentSource : DeploymentSourceBase<UserCohortsDeploymentStep>
{
    private const int MaxCohorts = 10_000;

    private readonly ICohortService _cohorts;
    private readonly UserManager<IUser> _userManager;

    public UserCohortsDeploymentSource(ICohortService cohorts, UserManager<IUser> userManager)
    {
        _cohorts = cohorts;
        _userManager = userManager;
    }

    protected override async Task ProcessAsync(UserCohortsDeploymentStep step, DeploymentPlanResult result)
    {
        var rows = new JsonArray();

        foreach (var cohort in await _cohorts.ListAsync(MaxCohorts))
        {
            var members = new JsonArray();

            foreach (var userId in await _cohorts.ListMemberIdsAsync(cohort.CohortId))
            {
                var user = await _userManager.FindByIdAsync(userId);

                if (user is not null)
                {
                    members.Add(user.UserName);
                }
            }

            rows.Add(new JsonObject
            {
                ["Id"] = cohort.CohortId,
                ["Name"] = cohort.Name,
                ["Description"] = cohort.Description,
                ["Members"] = members,
            });
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Cohorts"] = rows,
        });
    }
}

internal sealed class UserCohortsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, UserCohortsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(UserCohortsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("UserCohortsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("UserCohortsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(UserCohortsDeploymentStep step, BuildEditorContext context)
        => View("UserCohortsDeploymentStep_Fields_Edit", step).Location("Content");
}

internal sealed class UserCohortsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(UserCohortsDeploymentStep);

    protected override string DisplayText => "Cohorts";

    protected override string Description => "Exports every cohort with its people, by user name.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
