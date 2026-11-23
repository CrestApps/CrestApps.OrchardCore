using CrestApps.OrchardCore.MailingLists.Core.Coupons;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.MailingLists.Migrations;

/// <summary>
/// Makes the tenant's welcome coupon key when the feature is turned on, so two first sign-ups at once never make two
/// keys.
/// </summary>
internal sealed class WelcomeCouponMigrations : DataMigration
{
    private readonly MailingListCouponKeyStore _keys;

    public WelcomeCouponMigrations(MailingListCouponKeyStore keys)
    {
        _keys = keys;
    }

    public async Task<int> CreateAsync()
    {
        await _keys.EnsureKeyAsync(commit: false);

        return 1;
    }
}
