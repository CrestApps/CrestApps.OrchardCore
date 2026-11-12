using System.Security.Claims;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TimeTracker;

/// <summary>
/// Puts My time on a staff member's own page.
/// </summary>
[RequireFeatures(StaffConstants.Features.Staff)]
public sealed class StaffStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddStaffHomeLinkProvider<TimeTrackerStaffHomeLinks>();
    }
}

internal sealed class TimeTrackerStaffHomeLinks : IStaffHomeLinkProvider
{
    private readonly IAuthorizationService _authorizationService;

    internal readonly IStringLocalizer S;

    public TimeTrackerStaffHomeLinks(IAuthorizationService authorizationService, IStringLocalizer<TimeTrackerStaffHomeLinks> stringLocalizer)
    {
        _authorizationService = authorizationService;
        S = stringLocalizer;
    }

    public async Task<IEnumerable<StaffHomeLink>> GetLinksAsync(StaffMember staffMember, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => await _authorizationService.AuthorizeAsync(user, TimeTrackerPermissions.TrackOwnTime)
            ? [new StaffHomeLink(S["My time"], "~/Admin/time/my", S["Clock in and out, switch what you are doing, and see your day."], "fa-solid fa-stopwatch", 20)]
            : [];
}
