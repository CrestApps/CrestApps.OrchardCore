using System.Globalization;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.PointOfSale.Models;

namespace CrestApps.OrchardCore.PointOfSale.Services;

/// <summary>
/// Exports and anonymizes the register shifts a staff member opened or closed and the cash they moved. A shift keeps its
/// amounts, times and tallies, which cash reconciliation needs; the staff member's name is replaced, and their details
/// are scrubbed from the close note and the reasons given for cash movements.
/// </summary>
public sealed class RegisterShiftPersonalDataContributor : IPersonalDataContributor
{
    /// <summary>The record kind of a register shift.</summary>
    public const string RegisterShiftKind = "RegisterShift";

    // A privacy request reads at most this many shifts of each person.
    private const int MaxShifts = 5_000;

    private readonly IRegisterShiftStore _shifts;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterShiftPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="shifts">The register shift store.</param>
    public RegisterShiftPersonalDataContributor(IRegisterShiftStore shifts)
    {
        _shifts = shifts;
    }

    /// <inheritdoc/>
    public async Task ExportAsync(PersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var shift in await _shifts.ListForUsersAsync(context.Subject.UserIds, MaxShifts, cancellationToken))
        {
            var record = context.Add(RegisterShiftKind, shift.ItemId, shift.RegisterName, shift.OpenedUtc);

            record.Fields[nameof(RegisterShift.RegisterName)] = shift.RegisterName;
            record.Fields[nameof(RegisterShift.OpenedUtc)] = shift.OpenedUtc.ToString("O", CultureInfo.InvariantCulture);
            record.Fields[nameof(RegisterShift.ClosedUtc)] = shift.ClosedUtc?.ToString("O", CultureInfo.InvariantCulture);

            if (context.Subject.IsUser(shift.OpenedByUserId))
            {
                record.Fields[nameof(RegisterShift.OpenedByName)] = shift.OpenedByName;
            }

            if (context.Subject.IsUser(shift.ClosedByUserId))
            {
                record.Fields[nameof(RegisterShift.ClosedByName)] = shift.ClosedByName;
                record.Fields[nameof(RegisterShift.CountedCash)] = shift.CountedCash?.ToString(CultureInfo.InvariantCulture);
                record.Fields[nameof(RegisterShift.CloseNote)] = shift.CloseNote;
            }

            var movements = (shift.CashMovements ?? []).Where(movement => context.Subject.IsUser(movement.ByUserId)).ToArray();

            if (movements.Length > 0)
            {
                record.Fields[nameof(RegisterShift.CashMovements)] = string.Join(
                    Environment.NewLine,
                    movements.Select(movement => string.Create(CultureInfo.InvariantCulture, $"{movement.Utc:O} {movement.Amount} {movement.Reason}")));
            }
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(PersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var shift in await _shifts.ListForUsersAsync(context.Subject.UserIds, MaxShifts, cancellationToken))
        {
            if (context.Subject.IsUser(shift.OpenedByUserId))
            {
                shift.OpenedByName = context.ReplaceName(shift.OpenedByName);
            }

            if (context.Subject.IsUser(shift.ClosedByUserId))
            {
                shift.ClosedByName = context.ReplaceName(shift.ClosedByName);
            }

            shift.CloseNote = context.Scrub(shift.CloseNote);

            foreach (var movement in shift.CashMovements ?? [])
            {
                if (context.Subject.IsUser(movement.ByUserId))
                {
                    movement.ByName = context.ReplaceName(movement.ByName);
                }

                movement.Reason = context.Scrub(movement.Reason);
            }

            await _shifts.UpdateAsync(shift, cancellationToken);
            context.Result.Count(RegisterShiftKind);
        }
    }
}
