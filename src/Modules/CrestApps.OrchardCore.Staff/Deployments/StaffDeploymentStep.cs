using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Staff.Recipes;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Users;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Staff.Deployments;

/// <summary>
/// Exports staff members.
/// </summary>
public sealed class StaffDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<StaffDeploymentStep>("Organization");
    private static readonly LocalizationSource _title = LocalizationSource.Create<StaffDeploymentStep>("Staff");

    /// <inheritdoc />
    public override string Name => StaffConstants.RecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets whether the date of birth is exported.
    /// </summary>
    public bool IncludeSensitiveData { get; set; }
}

internal sealed class StaffDeploymentSource : DeploymentSourceBase<StaffDeploymentStep>
{
    private readonly IStaffMemberManager _staffManager;
    private readonly ITeamManager _teamManager;
    private readonly IUserSearchService _userSearchService;

    public StaffDeploymentSource(
        IStaffMemberManager staffManager,
        ITeamManager teamManager,
        IUserSearchService userSearchService)
    {
        _staffManager = staffManager;
        _teamManager = teamManager;
        _userSearchService = userSearchService;
    }

    protected override async Task ProcessAsync(StaffDeploymentStep step, DeploymentPlanResult result)
    {
        var teams = (await _teamManager.ListTreeAsync()).ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);
        var people = new List<Models.StaffMember>();

        for (var skip = 0; ; skip += 500)
        {
            var (count, page) = await _staffManager.SearchAsync(new StaffQuery(), skip, 500);

            people.AddRange(page);

            if (people.Count >= count || page.Count == 0)
            {
                break;
            }
        }

        var users = (await _userSearchService.GetAsync(people.Select(person => person.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)))
            .ToDictionary(user => user.UserId, user => user.UserName, StringComparer.Ordinal);

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Staff"] = new JsonArray([.. people.Select(person =>
            {
                var node = JsonSerializer.SerializeToNode(person, StaffRecipeStep.JsonOptions).AsObject();

                node.Remove("id");
                node.Remove("createdUtc");
                node.Remove("modifiedUtc");
                node.Remove("properties");
                node.Remove("userId");
                node.Remove("teamMemberships");
                node.Remove("primaryTeamId");

                if (!step.IncludeSensitiveData)
                {
                    node.Remove("dateOfBirth");
                }

                if (!string.IsNullOrEmpty(person.UserId) && users.TryGetValue(person.UserId, out var userName))
                {
                    node["userName"] = userName;
                }

                node["teams"] = new JsonArray([.. person.TeamMemberships.Select(membership =>
                {
                    var team = JsonSerializer.SerializeToNode(membership, StaffRecipeStep.JsonOptions).AsObject();

                    team.Remove("teamId");
                    team["teamName"] = teams.GetValueOrDefault(membership.TeamId, membership.TeamId);

                    return (JsonNode)team;
                })]);

                return (JsonNode)node;
            })]),
        });
    }
}

internal sealed class StaffDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, StaffDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(StaffDeploymentStep step, BuildDisplayContext context)
    {
        return CombineAsync(
            View("StaffDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("StaffDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));
    }

    public override IDisplayResult Edit(StaffDeploymentStep step, BuildEditorContext context)
    {
        return Initialize<StaffDeploymentStepViewModel>("StaffDeploymentStep_Fields_Edit", model => model.IncludeSensitiveData = step.IncludeSensitiveData).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(StaffDeploymentStep step, UpdateEditorContext context)
    {
        var model = new StaffDeploymentStepViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);
        step.IncludeSensitiveData = model.IncludeSensitiveData;

        return Edit(step, context);
    }
}

public class StaffDeploymentStepViewModel
{
    public bool IncludeSensitiveData { get; set; }
}

internal sealed class StaffDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(StaffDeploymentStep);

    protected override string DisplayText => "Staff";

    protected override string Description => "Exports every staff member, with teams by name and the user by user name.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        return [("IncludeSensitiveData", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the date of birth is exported."))];
    }
}
