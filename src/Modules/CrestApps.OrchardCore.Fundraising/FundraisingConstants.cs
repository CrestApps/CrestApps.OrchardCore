using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Fundraising;

public static class FundraisingConstants
{
    public const string Feature = "CrestApps.OrchardCore.Fundraising";

    /// <summary>
    /// The YesSql collection that holds donations.
    /// </summary>
    public const string CollectionName = "Fundraising";

    public const string SettingsGroupId = "fundraising";

    /// <summary>
    /// The cart line detail holding the amount a donor chose.
    /// </summary>
    public const string AmountDetail = "donation.amount";

    /// <summary>
    /// The cart line detail naming who a donation honors.
    /// </summary>
    public const string TributeDetail = "donation.tribute";
}

public static class FundraisingPermissions
{
    /// <summary>
    /// Sets who receipts are from, and sees donations.
    /// </summary>
    public static readonly Permission ManageFundraising = new("ManageFundraising", "Manage fundraising");
}

internal sealed class FundraisingPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        FundraisingPermissions.ManageFundraising,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
