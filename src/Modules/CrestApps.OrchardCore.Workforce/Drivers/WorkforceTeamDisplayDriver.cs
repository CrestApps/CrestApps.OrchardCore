using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Workforce.Geo;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// The Workforce section of the team editor: the site address, and, when their features are on, the time clock's site
/// coordinates and policy, how open shifts are given, whether shift trades need approval and what approved time off
/// does to shifts.
/// </summary>
internal sealed class WorkforceTeamDisplayDriver : DisplayDriver<Team>
{
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IEnumerable<ITimeClockPolicyManager> _timeClockPolicyManagers;

    internal readonly IStringLocalizer S;

    public WorkforceTeamDisplayDriver(
        IShellFeaturesManager shellFeaturesManager,
        IEnumerable<ITimeClockPolicyManager> timeClockPolicyManagers,
        IStringLocalizer<WorkforceTeamDisplayDriver> stringLocalizer)
    {
        _shellFeaturesManager = shellFeaturesManager;
        _timeClockPolicyManagers = timeClockPolicyManagers;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(Team team, BuildEditorContext context)
    {
        var features = await GetEnabledFeaturesAsync();
        var showShiftPlanning = features.Contains(WorkforceConstants.Features.ShiftPlanning);
        var showTimeOff = showShiftPlanning && features.Contains(WorkforceConstants.Features.TimeOff);
        var showTimeClock = features.Contains(WorkforceConstants.Features.TimeClock);
        var settings = team.GetWorkforceSettings();
        IList<SelectListItem> policyOptions = null;

        // The policy manager is registered only while the time clock is on.
        if (showTimeClock && _timeClockPolicyManagers.FirstOrDefault() is { } policies)
        {
            policyOptions = (await policies.ListEnabledAsync())
                .Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == settings.TimeClockPolicyId))
                .ToList();
        }

        void Populate(WorkforceTeamEditViewModel model)
        {
            model.TeamAddress = WorkforceGeocodeTargets.AddressText(team.Address);
            model.Latitude = settings.Location?.Latitude;
            model.Longitude = settings.Location?.Longitude;
            model.TimeClockPolicyId = settings.TimeClockPolicyId;
            model.OpenShiftClaimPolicy = settings.OpenShiftClaimPolicy?.ToString();
            model.ShiftTradesNeedApproval = settings.ShiftTradesNeedApproval.HasValue
                ? (settings.ShiftTradesNeedApproval.Value ? "true" : "false")
                : null;
            model.TimeOffShiftHandling = settings.TimeOffShiftHandling?.ToString();
            model.ShowShiftPlanning = showShiftPlanning;
            model.ShowTimeOff = showTimeOff;
            model.ShowTimeClock = showTimeClock;

            if (policyOptions is not null)
            {
                model.TimeClockPolicyOptions = policyOptions;
            }
        }

        var results = new List<IDisplayResult>
        {
            Initialize<WorkforceTeamEditViewModel>("WorkforceTeamSite_Edit", Populate)
                .Location(l => l.Zone("Content", "5").Card(S["Work Site"], "50")),
        };

        if (showTimeClock)
        {
            results.Add(Initialize<WorkforceTeamEditViewModel>("WorkforceTeamTimeClock_Edit", Populate)
                .Location(l => l.Zone("Content", "5").Card(S["Time Clock"], "51")));
        }

        if (showShiftPlanning)
        {
            results.Add(Initialize<WorkforceTeamEditViewModel>("WorkforceTeamShifts_Edit", Populate)
                .Location(l => l.Zone("Content", "5").Card(S["Shift Planning"], "52")));
        }

        return Combine([.. results]);
    }

    public override async Task<IDisplayResult> UpdateAsync(Team team, UpdateEditorContext context)
    {
        var model = new WorkforceTeamEditViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var features = await GetEnabledFeaturesAsync();
        var settings = team.GetWorkforceSettings();

        if (features.Contains(WorkforceConstants.Features.ShiftPlanning))
        {
            settings.OpenShiftClaimPolicy = Enum.TryParse<OpenShiftClaimPolicy>(model.OpenShiftClaimPolicy, out var policy) ? policy : null;
            settings.ShiftTradesNeedApproval = bool.TryParse(model.ShiftTradesNeedApproval, out var needsApproval) ? needsApproval : null;

            if (features.Contains(WorkforceConstants.Features.TimeOff))
            {
                settings.TimeOffShiftHandling = Enum.TryParse<TimeOffShiftHandling>(model.TimeOffShiftHandling, out var handling) ? handling : null;
            }
        }

        var timeClockOn = features.Contains(WorkforceConstants.Features.TimeClock);
        var latitude = timeClockOn ? model.Latitude : settings.Location?.Latitude;
        var longitude = timeClockOn ? model.Longitude : settings.Location?.Longitude;

        if (timeClockOn)
        {
            settings.TimeClockPolicyId = string.IsNullOrEmpty(model.TimeClockPolicyId) ? null : model.TimeClockPolicyId;

            if (latitude is < -90 or > 90 || longitude is < -180 or > 180 || latitude.HasValue != longitude.HasValue)
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.Latitude), S["Give both the latitude (-90 to 90) and the longitude (-180 to 180), or neither."]);
            }
        }

        settings.Location = latitude.HasValue
            ? new WorkforceLocation { Latitude = latitude, Longitude = longitude }
            : null;

        return await EditAsync(team, context);
    }

    private async Task<HashSet<string>> GetEnabledFeaturesAsync()
    {
        return (await _shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}
