using System.Security.Claims;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Payroll;

/// <summary>
/// My Pay: the person's own statements, stubs, elections and net pay split, linked from Staff &gt; My Profile.
/// </summary>
[Feature(PayrollConstants.Features.SelfService)]
public sealed class MyPayStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<MyPayStatementRow, MyPayStatementRowDisplayDriver>();
        services.AddDisplayDriver<MyPayTaxFormViewModel, CrestApps.OrchardCore.Payroll.Drivers.MyPayTaxFormDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IStaffHomeLinkProvider, MyPayStaffHomeLinks>());
    }
}

/// <summary>
/// Puts My Pay on a staff member's own page.
/// </summary>
internal sealed class MyPayStaffHomeLinks : IStaffHomeLinkProvider
{
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public MyPayStaffHomeLinks(IAuthorizationService authorizationService, IStringLocalizer<MyPayStaffHomeLinks> stringLocalizer)
    {
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<StaffHomeLink>> GetLinksAsync(StaffMember staffMember, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (!await _authorizationService.AuthorizeAsync(user, PayrollPermissions.ViewOwnPay))
        {
            return [];
        }

        return [new StaffHomeLink(S["My pay"], "~/Admin/my-pay", S["Your pay statements, tax elections and where your pay goes."], "fa-solid fa-money-check-dollar", 20)];
    }
}

/// <summary>
/// The rows of My Pay's statements: the pay date and employer, the net pay and status, and the stub.
/// </summary>
internal sealed class MyPayStatementRowDisplayDriver : DisplayDriver<MyPayStatementRow>
{
    public override Task<IDisplayResult> DisplayAsync(MyPayStatementRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        return CombineAsync(
            View("MyPayStatementRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MyPayStatementRow_Tags_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("MyPayStatementRow_Buttons_SummaryAdmin", row).Location("Actions:5"));
    }
}
