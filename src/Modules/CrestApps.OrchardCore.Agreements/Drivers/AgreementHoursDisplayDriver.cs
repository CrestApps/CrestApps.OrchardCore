using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Agreements.Services;
using CrestApps.OrchardCore.Agreements.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Agreements.Drivers;

/// <summary>
/// The hours an agreement covers on its editor, with how they stand in the current period.
/// </summary>
internal sealed class AgreementHoursDisplayDriver : DisplayDriver<Agreement>
{
    private readonly IAgreementBalanceService _balances;

    public AgreementHoursDisplayDriver(IAgreementBalanceService balances)
    {
        _balances = balances;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(Agreement agreement, BuildEditorContext context)
        => Initialize<AgreementHoursViewModel>("AgreementHours_Edit", async model =>
        {
            var hours = agreement.Hours ?? new AgreementHours();

            model.Kind = hours.Kind;
            model.Hours = hours.Hours;
            model.RoundingMinutes = hours.RoundingMinutes;

            if (!context.IsNew && hours.Kind != AgreementHoursKind.None)
            {
                model.Balance = await _balances.GetBalanceAsync(agreement);
            }
        }).Location("Content:2");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(Agreement agreement, UpdateEditorContext context)
    {
        var model = new AgreementHoursViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        agreement.Hours = new AgreementHours
        {
            Kind = model.Kind,
            Hours = model.Kind == AgreementHoursKind.None ? 0 : model.Hours,
            RoundingMinutes = model.RoundingMinutes,
        };

        return Edit(agreement, context);
    }
}
