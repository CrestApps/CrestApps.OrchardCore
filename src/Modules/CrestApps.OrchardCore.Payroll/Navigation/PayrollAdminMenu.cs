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
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-money-check-dollar")
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
        var filings = await AnyAsync(user, PayrollPermissions.ManagePayrollFilings);

        if (runs || profiles || employers || filings)
        {
            builder.Add(S["Payroll"], S["Payroll"].PrefixPosition("6"), payroll =>
            {
                payroll.Id("payroll").AddClass("payroll").AddClass("icon-class-fa-solid").AddClass("icon-class-fa-money-check-dollar");

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

                if (filings)
                {
                    payroll.Add(S["Liabilities and Filings"], S["Liabilities and Filings"].PrefixPosition("40"), group => group
                        .Id("payrollFilings")
                        .Add(S["Liabilities"], S["Liabilities"].PrefixPosition("10"), item => item
                            .Id("payrollLiabilities")
                            .Action("Liabilities", "PayrollFilings", PayrollConstants.Features.Payroll)
                            .LocalNav())
                        .Add(S["Filings"], S["Filings"].PrefixPosition("20"), item => item
                            .Id("payrollFilingsList")
                            .Action("Index", "PayrollFilings", PayrollConstants.Features.Payroll)
                            .LocalNav())
                        .Add(S["Year end"], S["Year end"].PrefixPosition("30"), item => item
                            .Id("payrollYearEnd")
                            .Action("YearEnd", "PayrollFilings", PayrollConstants.Features.Payroll)
                            .LocalNav()));
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

/// <summary>
/// Adds Payroll &gt; Garnishments for a user who manages the garnishments of some employer.
/// </summary>
public sealed class GarnishmentsAdminMenu : AdminNavigationProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPayrollScopeService _scope;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="GarnishmentsAdminMenu"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="scope">The payroll scope.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public GarnishmentsAdminMenu(IHttpContextAccessor httpContextAccessor, IPayrollScopeService scope, IStringLocalizer<GarnishmentsAdminMenu> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _scope = scope;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        // Shown to a user who manages the garnishments of some employer.
        if (_httpContextAccessor.HttpContext?.User is not { } user || (await _scope.GetScopeAsync(user, PayrollPermissions.ManageGarnishments)).IsEmpty)
        {
            return;
        }

        builder.Add(S["Payroll"], S["Payroll"].PrefixPosition("6"), payroll => payroll
            .Id("payroll")
            .AddClass("payroll")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-money-check-dollar")
            .Add(S["Garnishments"], S["Garnishments"].PrefixPosition("30"), item => item
                .Id("payrollGarnishments")
                .Action("Index", "Garnishments", PayrollConstants.Features.Payroll)
                .LocalNav()));
    }
}
