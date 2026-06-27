using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Permissions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;

/// <summary>
/// Adds the Phone Number Verifications settings and report entries to the admin navigation menu.
/// </summary>
internal sealed class PhoneNumberVerificationsAdminMenu : AdminNavigationProvider
{
    private static readonly RouteValueDictionary _settingsRouteValues = new()
    {
        { "area", "OrchardCore.Settings" },
        { "groupId", PhoneNumberVerificationsConstants.SettingsGroupIds.Verifications },
    };

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhoneNumberVerificationsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PhoneNumberVerificationsAdminMenu(IStringLocalizer<PhoneNumberVerificationsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Reports"], S["Reports"].PrefixPosition(), reports => reports
                .Add(S["Phone Number Verifications"], S["Phone Number Verifications"].PrefixPosition(), verifications => verifications
                    .AddClass("reports")
                    .Id("reports")
                    .Action("Index", "Reports", PhoneNumberVerificationsConstants.Features.Area)
                    .Permission(PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport)
                    .LocalNav()
                )
                .Add(S["Phone Number Verification Records"], S["Phone Number Verification Records"].PrefixPosition(), records => records
                    .AddClass("phone-number-verification-records")
                    .Id("phoneNumberVerificationRecords")
                    .Action("Index", "Records", PhoneNumberVerificationsConstants.Features.Area)
                    .Permission(PhoneNumberVerificationsPermissions.RunPhoneNumberVerificationsReport)
                    .LocalNav()
                ));

        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Phone Number Verifications"], S["Phone Number Verifications"].PrefixPosition(), verifications => verifications
                    .AddClass("phone-number-verifications-settings")
                    .Id("phoneNumberVerificationsSettings")
                    .Action("Index", "Admin", _settingsRouteValues)
                    .Permission(PhoneNumberVerificationsPermissions.ManagePhoneNumberVerificationSettings)
                    .LocalNav()
                )
            );

        return ValueTask.CompletedTask;
    }
}
