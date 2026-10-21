using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// The setup guide's staff roles: adds roles for the usual jobs in a store, so staff can be given one instead of picking
/// permissions. Orchard Core grants each new role the permissions the enabled modules declare for it in their default
/// stereotypes. A role that's there already is left as it is.
/// </summary>
internal sealed class StaffRolesSetupStep : IStoreSetupStep
{
    private readonly RoleManager<IRole> _roleManager;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public StaffRolesSetupStep(
        RoleManager<IRole> roleManager,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<StaffRolesSetupStep> stringLocalizer)
    {
        _roleManager = roleManager;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public string Name
        => "staff-roles";

    public int Order
        => 90;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var missing = new List<string>();

        foreach (var preset in StaffRolePresets.All)
        {
            if (await _roleManager.FindByNameAsync(_roleManager.NormalizeKey(preset.Name)) is null)
            {
                missing.Add(preset.Name);
            }
        }

        return new StoreSetupStepDescription
        {
            Title = S["Add roles for your staff"],
            Description = S["Adds the roles Store manager, Warehouse, Customer support and Marketing, each allowed what that job needs. Give staff a role under Security, Users."],
            IsDone = missing.Count == 0,
            Status = missing.Count == 0 ? S["The staff roles are there."] : null,
            IsOptional = true,
            Url = _linkGenerator.GetPathByAction(_httpContextAccessor.HttpContext, "Index", "Admin", new { area = "OrchardCore.Roles" }),
            UrlText = S["Roles"],
            ActionText = missing.Count == 0 ? null : S["Add the staff roles"],
        };
    }

    public async Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
    {
        var added = new List<string>();

        foreach (var preset in StaffRolePresets.All)
        {
            if (await _roleManager.FindByNameAsync(_roleManager.NormalizeKey(preset.Name)) is not null)
            {
                continue;
            }

            // Orchard Core gives a new role the permissions every enabled module's stereotypes grant to its name.
            var role = new Role
            {
                RoleName = preset.Name,
                RoleDescription = preset.Description,
            };

            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                return StoreSetupActionResult.Failure(S["The role {0} couldn't be added: {1}", preset.Name, string.Join(" ", result.Errors.Select(error => error.Description))]);
            }

            added.Add(preset.Name);
        }

        return added.Count == 0
            ? StoreSetupActionResult.Success(S["The staff roles were there already."])
            : StoreSetupActionResult.Success(S["Added the roles {0}. Give staff one under Security, Users.", string.Join(", ", added)]);
    }
}

/// <summary>
/// The roles the setup guide adds for a store's staff. The guide only creates them: each commerce module grants its
/// permissions to these role names through its permission provider's default stereotypes, which Orchard Core applies
/// when a role is created and again when a module that grants it more is enabled later.
/// </summary>
public static class StaffRolePresets
{
    /// <summary>
    /// A staff role the setup guide adds.
    /// </summary>
    /// <param name="Name">The role's name, one of <see cref="CommerceConstants.StaffRoles"/>.</param>
    /// <param name="Description">What the role is for.</param>
    public sealed record Preset(string Name, string Description);

    public static readonly Preset StoreManager = new(
        CommerceConstants.StaffRoles.StoreManager,
        "Runs the store day to day: products, orders, stock, shipping, customers, promotions and reports.");

    public static readonly Preset Warehouse = new(
        CommerceConstants.StaffRoles.Warehouse,
        "Picks, packs and ships orders, and receives and counts stock.");

    public static readonly Preset CustomerSupport = new(
        CommerceConstants.StaffRoles.CustomerSupport,
        "Answers customers: looks up their orders, changes and refunds them, and manages subscriptions and bookings.");

    public static readonly Preset Marketing = new(
        CommerceConstants.StaffRoles.Marketing,
        "Runs promotions, campaigns, reviews and product feeds, and edits products and pages.");

    public static readonly IReadOnlyList<Preset> All = [StoreManager, Warehouse, CustomerSupport, Marketing];
}
