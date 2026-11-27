using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// Exports every audience administrators defined.
/// </summary>
public sealed class UserAudiencesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<UserAudiencesDeploymentStep>("Users");
    private static readonly LocalizationSource _title = LocalizationSource.Create<UserAudiencesDeploymentStep>("User audiences");

    /// <inheritdoc/>
    public override string Name => UsersConstants.AudiencesRecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class UserAudiencesDeploymentSource : DeploymentSourceBase<UserAudiencesDeploymentStep>
{
    private readonly IUserAudienceService _audiences;

    public UserAudiencesDeploymentSource(IUserAudienceService audiences)
    {
        _audiences = audiences;
    }

    protected override async Task ProcessAsync(UserAudiencesDeploymentStep step, DeploymentPlanResult result)
    {
        var rows = new JsonArray();

        foreach (var audience in (await _audiences.ListAsync()).Where(audience => audience.IsDefined))
        {
            var definition = await _audiences.FindDefinitionAsync(audience.Name);

            if (definition is not null)
            {
                rows.Add(JsonSerializer.SerializeToNode(definition, UserAudiencesRecipeStep.JsonOptions));
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Audiences"] = rows,
        });
    }
}

internal sealed class UserAudiencesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, UserAudiencesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public UserAudiencesDeploymentStepDisplayDriver(IStringLocalizer<UserAudiencesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(UserAudiencesDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("UserAudiencesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("UserAudiencesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(UserAudiencesDeploymentStep step, BuildEditorContext context)
        => View("UserAudiencesDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["User Audiences"], "10"));
}

internal sealed class UserAudiencesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(UserAudiencesDeploymentStep);

    protected override string DisplayText => "User audiences";

    protected override string Description => "Exports every audience administrators defined, with its rules.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
