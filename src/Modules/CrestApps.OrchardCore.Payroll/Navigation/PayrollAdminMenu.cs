using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Payroll.Navigation;

/// <summary>
/// Adds Payroll &gt; Setup to the admin menu: pay schedules and pay codes.
/// </summary>
public sealed class PaySetupAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaySetupAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PaySetupAdminMenu(IStringLocalizer<PaySetupAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Payroll"], S["Payroll"].PrefixPosition("6"), payroll => payroll
            .Id("payroll")
            .AddClass("payroll")
            .Add(S["Setup"], S["Setup"].PrefixPosition("90"), setup => setup
                .Id("payrollSetup")
                .Add(S["Pay Schedules"], S["Pay Schedules"].PrefixPosition("10"), schedules => schedules
                    .Id("paySchedules")
                    .Action("Index", "PaySchedules", PayrollConstants.Features.Payroll)
                    .Permission(PayrollPermissions.ManagePaySetup)
                    .LocalNav())
                .Add(S["Pay Codes"], S["Pay Codes"].PrefixPosition("20"), codes => codes
                    .Id("payCodes")
                    .Action("Index", "PayCodes", PayrollConstants.Features.Payroll)
                    .Permission(PayrollPermissions.ManagePaySetup)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Payroll &gt; Pay Runs, Employees and Setup &gt; Employers, each shown only to users who hold its permission for at
/// least one employer (globally or for that employer), and the payroll settings.
/// </summary>
public sealed class PayrollAdminMenu : AdminNavigationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPayrollScopeService _scope;

    internal readonly IStringLocalizer S;

    public PayrollAdminMenu(IHttpContextAccessor httpContextAccessor, IPayrollScopeService scope, IStringLocalizer<PayrollAdminMenu> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _scope = scope;
        S = stringLocalizer;
    }

    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        // The pages follow permissions per employer, which a menu permission can't express: ask the payroll scope.
        var user = _httpContextAccessor.HttpContext?.User;
        var runs = await AnyAsync(user, PayrollPermissions.ViewPayRuns);
        var profiles = await AnyAsync(user, PayrollPermissions.ViewPayProfiles);
        var employers = await AnyAsync(user, PayrollPermissions.ManagePayrollSettings);

        if (runs || profiles || employers)
        {
            builder.Add(S["Payroll"], S["Payroll"].PrefixPosition("6"), payroll =>
            {
                payroll.Id("payroll");

                if (runs)
                {
                    payroll.Add(S["Pay Runs"], S["Pay Runs"].PrefixPosition("10"), item => item
                        .Id("payrollPayRuns")
                        .Action("Index", "PayRuns", PayrollConstants.Features.Payroll)
                        .LocalNav());
                }

                if (profiles)
                {
                    payroll.Add(S["Employees"], S["Employees"].PrefixPosition("20"), item => item
                        .Id("payrollEmployees")
                        .Action("Index", "PayProfiles", PayrollConstants.Features.Payroll)
                        .LocalNav());
                }

                if (employers)
                {
                    payroll.Add(S["Setup"], S["Setup"].PrefixPosition("90"), setup => setup
                        .Id("payrollSetup")
                        .Add(S["Employers"], S["Employers"].PrefixPosition("5"), item => item
                            .Id("payrollEmployers")
                            .Action("Index", "Employers", PayrollConstants.Features.Payroll)
                            .LocalNav()));
                }
            });
        }

        builder.Add(S["Settings"], settings => settings
            .Add(S["Payroll"], S["Payroll"].PrefixPosition(), payroll => payroll
                .Id("payrollSettings")
                .Action("Index", "Admin", new Microsoft.AspNetCore.Routing.RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", PayrollConstants.SettingsGroupId } })
                .Permission(PayrollPermissions.ManagePayrollSettings)
                .LocalNav()));
    }

    private async Task<bool> AnyAsync(System.Security.Claims.ClaimsPrincipal user, global::OrchardCore.Security.Permissions.Permission permission)
        => user?.Identity?.IsAuthenticated == true && !(await _scope.GetScopeAsync(user, permission)).IsEmpty;
}

public sealed class PayrollTaxTablesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaxTablesAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayrollTaxTablesAdminMenu(IStringLocalizer<PayrollTaxTablesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Payroll"], S["Payroll"].PrefixPosition("6"), payroll => payroll
            .Add(S["Setup"], S["Setup"].PrefixPosition("90"), setup => setup
                .Id("payrollSetup")
                .Add(S["Tax rules"], S["Tax rules"].PrefixPosition("30"), rules => rules
                    .Id("payrollTaxRules")
                    .Action("Index", "PayrollTaxRules", PayrollConstants.Features.Payroll)
                    .Permission(PayrollPermissions.ManagePayrollSettings)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
