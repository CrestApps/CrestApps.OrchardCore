using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// The setup guide's staff roles: adds roles for the usual jobs in a store, each with the permissions it needs, so
/// staff can be given one instead of picking permissions. A role that's there already is left as it is.
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

            var role = new Role
            {
                RoleName = preset.Name,
                RoleDescription = preset.Description,
            };

            foreach (var permission in preset.Permissions)
            {
                role.RoleClaims.Add(new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = permission });
            }

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
/// The roles the setup guide adds for a store's staff, with the permissions of each. A permission of a feature that
/// isn't on does nothing until the feature is.
/// </summary>
public static class StaffRolePresets
{
    public sealed record Preset(string Name, string Description, IReadOnlyList<string> Permissions);

    private const string AccessAdminPanel = "AccessAdminPanel";

    public static readonly Preset StoreManager = new(
        "Store manager",
        "Runs the store day to day: products, orders, stock, shipping, customers, promotions and reports.",
        [
            AccessAdminPanel, "ListContent", "ViewContent", "EditContent", "PublishContent", "DeleteContent", "ManageMediaContent",
            "ManageOrders", "ManageGiftCards", "ManageRefunds", "ManageTransactions",
            "ManageInventory", "ViewInventory", "ManageShipping", "FulfillOrders",
            "ManageCustomers", "ViewCustomers", "ManageCoupons", "ManageMarketing", "ManageProductReviews", "ManageProductFeeds",
            "ManageSubscriptions", "ManageBookings", "ManageMemberships", "ManageDigitalDownloads", "ManageFundraising",
            "ModerateClassifieds", "ViewAllReportDesigns",
        ]);

    public static readonly Preset Warehouse = new(
        "Warehouse",
        "Picks, packs and ships orders, and receives and counts stock.",
        [AccessAdminPanel, "FulfillOrders", "ViewInventory", "ManageInventory"]);

    public static readonly Preset CustomerSupport = new(
        "Customer support",
        "Answers customers: looks up their orders, changes and refunds them, and manages subscriptions and bookings.",
        [AccessAdminPanel, "ManageOrders", "ManageRefunds", "ViewCustomers", "ManageCustomers", "ManageSubscriptions", "ManageBookings", "ManageMemberships", "ViewInventory"]);

    public static readonly Preset Marketing = new(
        "Marketing",
        "Runs promotions, campaigns, reviews and product feeds, and edits products and pages.",
        [
            AccessAdminPanel, "ListContent", "ViewContent", "EditContent", "PublishContent", "ManageMediaContent",
            "ManageMarketing", "ManageCoupons", "ManageProductReviews", "ManageProductFeeds", "ViewCustomers", "ViewAllReportDesigns",
        ]);

    public static readonly IReadOnlyList<Preset> All = [StoreManager, Warehouse, CustomerSupport, Marketing];
}
