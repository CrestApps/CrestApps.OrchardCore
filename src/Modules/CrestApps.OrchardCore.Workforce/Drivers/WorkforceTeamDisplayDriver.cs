using CrestApps.OrchardCore.Teams.Models;
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

    public override IDisplayResult Edit(Team team, BuildEditorContext context)
    {
        return Initialize<WorkforceTeamEditViewModel>("WorkforceTeamFields_Edit", async model =>
        {
            var settings = team.GetWorkforceSettings();

            model.Address = settings.Location?.Address;
            model.Latitude = settings.Location?.Latitude;
            model.Longitude = settings.Location?.Longitude;
            model.TimeClockPolicyId = settings.TimeClockPolicyId;
            model.OpenShiftClaimPolicy = settings.OpenShiftClaimPolicy?.ToString();
            model.ShiftTradesNeedApproval = settings.ShiftTradesNeedApproval.HasValue
                ? (settings.ShiftTradesNeedApproval.Value ? "true" : "false")
                : null;
            model.TimeOffShiftHandling = settings.TimeOffShiftHandling?.ToString();

            var features = await GetEnabledFeaturesAsync();

            model.ShowShiftPlanning = features.Contains(WorkforceConstants.Features.ShiftPlanning);
            model.ShowTimeOff = model.ShowShiftPlanning && features.Contains(WorkforceConstants.Features.TimeOff);
            model.ShowTimeClock = features.Contains(WorkforceConstants.Features.TimeClock);

            // The policy manager is registered only while the time clock is on.
            if (model.ShowTimeClock && _timeClockPolicyManagers.FirstOrDefault() is { } policies)
            {
                model.TimeClockPolicyOptions = (await policies.ListEnabledAsync())
                    .Select(policy => new SelectListItem(policy.Name, policy.ItemId, policy.ItemId == model.TimeClockPolicyId))
                    .ToList();
            }
        }).Location("Content:5");
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

        settings.Location = string.IsNullOrWhiteSpace(model.Address) && !latitude.HasValue
            ? null
            : new WorkforceLocation { Address = model.Address?.Trim(), Latitude = latitude, Longitude = longitude };

        return Edit(team, context);
    }

    private async Task<HashSet<string>> GetEnabledFeaturesAsync()
    {
        return (await _shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}
