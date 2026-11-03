using CrestApps.OrchardCore.Workforce.Portal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the My Work menu: a worker's own schedule, timesheet, time off, shifts and settings, and a manager's approvals
/// and team. Each item shows only to the people it is for, as the pages themselves allow.
/// </summary>
public sealed class PortalAdminMenu : AdminNavigationProvider
{
    private readonly MyWorkPortal _portal;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalAdminMenu"/> class.
    /// </summary>
    /// <param name="portal">The portal, for what the signed-in person may use.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PortalAdminMenu(MyWorkPortal portal, IHttpContextAccessor httpContextAccessor, IStringLocalizer<PortalAdminMenu> stringLocalizer)
    {
        _portal = portal;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override async ValueTask BuildAsync(NavigationBuilder builder)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext?.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var user = httpContext.User;
        var worker = await _portal.GetWorkerAsync(user);
        var nav = await _portal.NavAsync(user, worker, null, httpContext.RequestAborted);

        if (!nav.IsWorker && !nav.IsManager)
        {
            return;
        }

        builder.Add(S["My Work"], S["My Work"].PrefixPosition("1"), myWork =>
        {
            myWork
                .Id("myWork")
                .AddClass("my-work")
                .AddClass("icon-class-fa-solid")
                .AddClass("icon-class-fa-user-clock")
                .Permission(WorkforcePermissions.UseWorkforcePortal);

            var position = 0;

            void Item(string id, LocalizedString title, string url)
            {
                position += 10;
                myWork.Add(title, title.PrefixPosition(position.ToString("00", System.Globalization.CultureInfo.InvariantCulture)), item => item
                    .Id(id)
                    .Url(url)
                    .Permission(WorkforcePermissions.UseWorkforcePortal)
                    .LocalNav());
            }

            Item("myWorkHome", nav.Unread > 0 ? S["Home ({0})", nav.Unread] : S["Home"], "~/Admin/my-work");

            if (nav.IsWorker)
            {
                Item("myWorkSchedule", S["Schedule"], "~/Admin/my-work/schedule");

                if (nav.HasTimeClock)
                {
                    Item("myWorkTimesheet", S["Timesheet"], "~/Admin/my-work/timesheet");
                }

                if (nav.HasTimeOff)
                {
                    Item("myWorkTimeOff", S["Time off"], "~/Admin/my-work/time-off");
                }

                if (nav.HasShiftPlanning)
                {
                    Item("myWorkShifts", S["Shifts"], "~/Admin/my-work/shifts");
                }

                foreach (var link in nav.Links)
                {
                    Item("myWork-" + link.Key, link.Badge > 0 ? S["{0} ({1})", link.Title, link.Badge] : S["{0}", link.Title], link.Path);
                }

                Item("myWorkAvailability", S["Availability"], "~/Admin/my-work/availability");
                Item("myWorkSettings", S["Settings"], "~/Admin/my-work/settings");
            }

            if (nav.IsManager)
            {
                Item("myWorkApprovals", S["Approvals"], "~/Admin/my-work/approvals");
                Item("myWorkTeam", S["Who's working"], "~/Admin/my-work/team");
            }
        });
    }
}
