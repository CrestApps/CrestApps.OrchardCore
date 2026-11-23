using CrestApps.OrchardCore.MailingLists.Migrations;
using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.MailingLists.WelcomeCoupon;

/// <summary>
/// Registers the welcome coupon: its card on the list editor, the observer that issues the codes, and the migration that
/// makes the tenant's coupon key.
/// </summary>
[Feature(MailingListsConstants.Features.WelcomeCoupon)]
public sealed class WelcomeCouponStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<MailingList, MailingListWelcomeCouponDisplayDriver>();
        services.AddMailingListMemberObserver<MailingListWelcomeCouponObserver>();
        services.AddDataMigration<WelcomeCouponMigrations>();
    }
}
