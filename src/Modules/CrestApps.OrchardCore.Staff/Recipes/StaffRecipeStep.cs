using CrestApps.OrchardCore.Staff.Core.Services;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using Json.Schema;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Staff.Recipes;

/// <summary>
/// Imports staff members: each is matched by id, then employee number, then user, and created when none matches.
/// Teams are given by name, the user by user name, and the manager by id or employee number (listed in any order).
/// </summary>
internal sealed class StaffRecipeStep : NamedRecipeStepHandler
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IStaffMemberManager _staffManager;
    private readonly StaffTeamReader _teamManager;
    private readonly UserManager<IUser> _userManager;

    public StaffRecipeStep(
        IStaffMemberManager staffManager,
        StaffTeamReader teamManager,
        UserManager<IUser> userManager)
        : base(StaffConstants.RecipeStepName)
    {
        _staffManager = staffManager;
        _teamManager = teamManager;
        _userManager = userManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var nodes = (context.Step["Staff"] as JsonArray ?? []).OfType<JsonObject>().ToArray();
        var teams = (await _teamManager.ListTreeAsync()).Select(item => item.Team).ToArray();
        var saved = new List<(StaffMember Staff, JsonObject Node)>();

        foreach (var node in nodes)
        {
            var itemId = node["itemId"]?.GetValue<string>();
            var employeeNumber = node["employeeNumber"]?.GetValue<string>();
            var userName = node["userName"]?.GetValue<string>();
            string userId = null;

            if (!string.IsNullOrWhiteSpace(userName) && await _userManager.FindByNameAsync(userName) is IUser user)
            {
                userId = await _userManager.GetUserIdAsync(user);
            }

            var staff = (string.IsNullOrEmpty(itemId) ? null : await _staffManager.FindByIdAsync(itemId))
                ?? await _staffManager.FindByEmployeeNumberAsync(employeeNumber)
                ?? await _staffManager.FindByUserIdAsync(userId);
            var isNew = staff is null;

            if (isNew)
            {
                staff = await _staffManager.NewAsync();

                if (!string.IsNullOrEmpty(itemId))
                {
                    staff.ItemId = itemId;
                }
            }

            var values = node.Deserialize<StaffMember>(JsonOptions) ?? new StaffMember();

            staff.DisplayName = values.DisplayName ?? staff.DisplayName;
            staff.UserId = userId ?? staff.UserId;
            staff.Email = values.Email ?? staff.Email;
            staff.Phone = values.Phone ?? staff.Phone;
            staff.EmployeeNumber = values.EmployeeNumber ?? staff.EmployeeNumber;
            staff.ExternalId = values.ExternalId ?? staff.ExternalId;
            staff.JobTitle = values.JobTitle ?? staff.JobTitle;
            staff.EmploymentType = node.ContainsKey("employmentType") ? values.EmploymentType : staff.EmploymentType;
            staff.Status = node.ContainsKey("status") ? values.Status : staff.Status;
            staff.HireDate = values.HireDate ?? staff.HireDate;
            staff.SeniorityDate = values.SeniorityDate ?? staff.SeniorityDate;
            staff.TerminationDate = values.TerminationDate ?? staff.TerminationDate;
            staff.TimeZoneId = values.TimeZoneId ?? staff.TimeZoneId;
            staff.WorkLocation = values.WorkLocation ?? staff.WorkLocation;
            staff.HomeBase = values.HomeBase ?? staff.HomeBase;
            staff.ContractedHoursPerWeek = values.ContractedHoursPerWeek ?? staff.ContractedHoursPerWeek;

            if (node["capabilities"] is JsonArray)
            {
                staff.Capabilities = values.Capabilities;
            }

            // Team memberships are only read while the Teams module is on.
            if (_teamManager.UsesTeams && node["teams"] is JsonArray teamNodes)
            {
                staff.TeamMemberships = [];

                foreach (var teamNode in teamNodes.OfType<JsonObject>())
                {
                    var teamName = teamNode["teamName"]?.GetValue<string>();
                    var teamId = teamNode["teamId"]?.GetValue<string>()
                        ?? teams.FirstOrDefault(team => string.Equals(team.Name, teamName, StringComparison.OrdinalIgnoreCase))?.ItemId;

                    if (string.IsNullOrEmpty(teamId))
                    {
                        context.Errors.Add($"{StaffConstants.RecipeStepName}: the team '{teamName}' of '{staff.DisplayName}' doesn't exist.");

                        continue;
                    }

                    var membership = teamNode.Deserialize<TeamMembership>(JsonOptions) ?? new TeamMembership();

                    membership.TeamId = teamId;
                    staff.TeamMemberships.Add(membership);
                }

                staff.PrimaryTeamId = staff.TeamMemberships.FirstOrDefault()?.TeamId;
            }

            // The manager is set once everyone is saved, since they may be listed after.
            var managerId = staff.ManagerId;

            staff.ManagerId = null;

            var validation = await _staffManager.ValidateAsync(staff);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"{StaffConstants.RecipeStepName}: '{staff.DisplayName}' wasn't imported: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            if (isNew)
            {
                await _staffManager.CreateAsync(staff);
            }
            else
            {
                staff.ManagerId = managerId;
                await _staffManager.UpdateAsync(staff);
            }

            saved.Add((staff, node));
        }

        foreach (var (staff, node) in saved)
        {
            var managerId = node["managerId"]?.GetValue<string>();
            var managerNumber = node["managerEmployeeNumber"]?.GetValue<string>();

            if (string.IsNullOrEmpty(managerId) && string.IsNullOrEmpty(managerNumber))
            {
                continue;
            }

            var manager = (string.IsNullOrEmpty(managerId) ? null : await _staffManager.FindByIdAsync(managerId))
                ?? await _staffManager.FindByEmployeeNumberAsync(managerNumber);

            if (manager is null)
            {
                context.Errors.Add($"{StaffConstants.RecipeStepName}: the manager of '{staff.DisplayName}' doesn't exist.");

                continue;
            }

            staff.ManagerId = manager.ItemId;

            var validation = await _staffManager.ValidateAsync(staff);

            if (!validation.Succeeded)
            {
                context.Errors.Add($"{StaffConstants.RecipeStepName}: the manager of '{staff.DisplayName}' wasn't set: {string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))}");

                continue;
            }

            await _staffManager.UpdateAsync(staff);
        }
    }
}

/// <summary>
/// Describes the Staff recipe step to CrestApps Recipes.
/// </summary>
internal sealed class StaffRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => StaffConstants.RecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(StaffConstants.RecipeStepName).Description("Recipe step discriminator. Must be 'Staff'.")),
                ("Staff", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(Member()).Description("The staff members to create, or update by id, employee number or user.")))
            .Required("name")
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String).Description(description);

    private static JsonSchemaBuilder Member()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("itemId", Text("The staff member id; omit to match by employee number or user.")),
                ("displayName", Text("The name; taken from the user when one is given.")),
                ("userName", Text("The user name of the linked account.")),
                ("email", Text("The work e-mail.")),
                ("phone", Text("The work phone.")),
                ("employeeNumber", Text("The employee (badge) number; unique.")),
                ("externalId", Text("The id in an HR or payroll system.")),
                ("jobTitle", Text("The job title.")),
                ("employmentType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("FullTime", "PartTime", "Seasonal", "Temporary", "Contractor", "Subcontractor")),
                ("status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Active", "OnLeave", "Suspended", "Terminated")),
                ("hireDate", Text("The hire date, yyyy-MM-dd.")),
                ("seniorityDate", Text("The seniority date, yyyy-MM-dd.")),
                ("terminationDate", Text("The last working day, yyyy-MM-dd.")),
                ("managerId", Text("The id of the staff member they report to.")),
                ("managerEmployeeNumber", Text("The employee number of the staff member they report to.")),
                ("timeZoneId", Text("The IANA time zone.")),
                ("workLocation", Text("The place they work at.")),
                ("contractedHoursPerWeek", new JsonSchemaBuilder().Type(SchemaValueType.Number)),
                ("capabilities", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("Capabilities modules registered, such as Technician.")),
                ("teams", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("teamName", Text("The team name.")),
                        ("teamId", Text("The team id.")),
                        ("role", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Member", "Lead", "Scheduler", "Manager")),
                        ("effectiveFrom", Text("yyyy-MM-dd")),
                        ("effectiveTo", Text("yyyy-MM-dd")))
                    .AdditionalProperties(true))))
            .AdditionalProperties(true);
    }
}
