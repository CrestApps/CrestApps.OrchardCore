using CrestApps.OrchardCore.Workforce.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.Drivers;

/// <summary>
/// Builds the editor of a time off policy, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class TimeOffPolicyEditDisplayDriver : DisplayDriver<TimeOffPolicyEditViewModel>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffPolicyEditDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffPolicyEditDisplayDriver(IStringLocalizer<TimeOffPolicyEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TimeOffPolicyEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<TimeOffPolicyEditViewModel>("TimeOffPolicyDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<TimeOffPolicyEditViewModel>("TimeOffPolicyAccrual_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Accrual"], "20")),
            Initialize<TimeOffPolicyEditViewModel>("TimeOffPolicyLimits_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Limits"], "30")),
            Initialize<TimeOffPolicyEditViewModel>("TimeOffPolicyScope_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "4").Card(S["Applies To"], "40")));
    }

    /// <inheritdoc />
    public override async Task<IDisplayResult> UpdateAsync(TimeOffPolicyEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.TypeId, m => m.Enabled, m => m.AccrualMethod, m => m.Rate, m => m.Frequency, m => m.AnchorDate, m => m.TenureTiers, m => m.WaitingPeriodDays, m => m.MaxBalance, m => m.ResetMonth, m => m.ResetDay, m => m.CarryoverMax, m => m.NegativeBalanceLimit, m => m.TeamIds, m => m.EffectiveFrom, m => m.EffectiveTo);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the scripts of the page select.
    protected override void BuildPrefix(TimeOffPolicyEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(TimeOffPolicyEditViewModel source, TimeOffPolicyEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.TypeId = source.TypeId;
        model.AccrualMethod = source.AccrualMethod;
        model.Frequency = source.Frequency;
        model.AnchorDate = source.AnchorDate;
        model.Rate = source.Rate;
        model.TenureTiers = source.TenureTiers;
        model.WaitingPeriodDays = source.WaitingPeriodDays;
        model.MaxBalance = source.MaxBalance;
        model.CarryoverMax = source.CarryoverMax;
        model.ResetMonth = source.ResetMonth;
        model.ResetDay = source.ResetDay;
        model.NegativeBalanceLimit = source.NegativeBalanceLimit;
        model.TeamIds = source.TeamIds;
        model.EffectiveFrom = source.EffectiveFrom;
        model.EffectiveTo = source.EffectiveTo;
        model.Enabled = source.Enabled;
        model.WorkerAssignments = source.WorkerAssignments;
        model.TypeOptions = source.TypeOptions;
        model.TeamOptions = source.TeamOptions;
    }
}
