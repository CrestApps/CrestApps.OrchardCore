using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using CrestApps.OrchardCore.Staff.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PointOfSale.Payroll;

/// <summary>
/// Hands the tips cashiers declare at the close of a shift to payroll: the card tips on the Tips pay code (paid through
/// payroll) and the cash tips kept on the Cash tips reported pay code (taxed, not paid), for the staff member who opened
/// the shift, on the day it closed in the site's time zone. A pay run or an export that takes them marks the shift, and
/// its tips can no longer change; a voided run gives them back.
/// </summary>
internal sealed class PosTipsPayInputSource : IPayInputSource
{
    /// <summary>
    /// The kind of record tips come from.
    /// </summary>
    public const string Kind = "RegisterShift";

    private readonly IRegisterShiftStore _shifts;
    private readonly IStaffMemberManager _staff;
    private readonly IPayCodeManager _codes;
    private readonly ILocalClock _localClock;

    public PosTipsPayInputSource(IRegisterShiftStore shifts, IStaffMemberManager staff, IPayCodeManager codes, ILocalClock localClock)
    {
        _shifts = shifts;
        _staff = staff;
        _codes = codes;
        _localClock = localClock;
    }

    public string SourceKind => Kind;

    public async Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // A shift closes up to a day after it opens and the site's zone is within a day of UTC: read a wider window, then
        // keep the shifts closed on a day of the period.
        var fromUtc = query.PeriodStart?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-3);
        var toUtc = query.PeriodEnd?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(2);
        var shifts = new List<(RegisterShift Shift, DateOnly Day)>();

        foreach (var shift in await _shifts.ListClosedAsync(fromUtc, toUtc, cancellationToken))
        {
            if (shift.ClosedUtc is not { } closed || (shift.CardTips is not > 0m && shift.CashTips is not > 0m) || string.IsNullOrEmpty(shift.OpenedByUserId))
            {
                continue;
            }

            var day = DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(closed, DateTimeKind.Utc))).DateTime);
            var taken = PosShiftService.TipsTaken(shift);

            if ((query.PeriodStart.HasValue && day < query.PeriodStart.Value) || (query.PeriodEnd.HasValue && day > query.PeriodEnd.Value) || (!query.IncludeConsumed && taken))
            {
                continue;
            }

            shifts.Add((shift, day));
        }

        if (shifts.Count == 0)
        {
            return [];
        }

        var people = (await _staff.GetByUserIdsAsync(shifts.Select(item => item.Shift.OpenedByUserId).Distinct(StringComparer.Ordinal), cancellationToken))
            .Where(member => !string.IsNullOrEmpty(member.UserId))
            .GroupBy(member => member.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var codes = await _codes.GetAllAsync(cancellationToken);
        var card = Code(codes, "TIPS", PayCodeClass.Earning);
        var cash = Code(codes, "CTIPS", PayCodeClass.ImputedIncome);
        var inputs = new List<PayInput>();

        foreach (var (shift, day) in shifts)
        {
            if (people.GetValueOrDefault(shift.OpenedByUserId) is not { } member
                || (query.StaffMemberIds is { Count: > 0 } && !query.StaffMemberIds.Contains(member.ItemId)))
            {
                continue;
            }

            PayInput Input(PayCode code, decimal amount)
                => new()
                {
                    SourceKind = Kind,
                    SourceId = shift.ItemId,
                    StaffMemberId = member.ItemId,
                    StaffMemberName = member.DisplayName,
                    EmployeeNumber = member.EmployeeNumber,
                    PayrollId = member.ExternalId ?? member.EmployeeNumber,
                    PayScheduleId = query.PayScheduleId,
                    PeriodStart = query.PeriodStart ?? day,
                    PeriodEnd = query.PeriodEnd ?? day,
                    PayCodeId = code.ItemId,
                    Date = day,
                    Amount = Math.Round(amount, 2),
                    Consumed = PosShiftService.TipsTaken(shift),
                };

            if (shift.CardTips is > 0m && card is not null)
            {
                inputs.Add(Input(card, shift.CardTips.Value));
            }

            if (shift.CashTips is > 0m && cash is not null)
            {
                inputs.Add(Input(cash, shift.CashTips.Value));
            }
        }

        return inputs;
    }

    public async Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        foreach (var shift in await ShiftsAsync(sourceIds, cancellationToken))
        {
            var payroll = shift.TryGet<RegisterShiftPayroll>(out var stored) && stored is not null ? stored : new RegisterShiftPayroll();

            if (!payroll.ConsumerIds.Contains(consumerId))
            {
                payroll.ConsumerIds.Add(consumerId);
                shift.Put(payroll);
                await _shifts.UpdateAsync(shift, cancellationToken);
            }
        }
    }

    public async Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        foreach (var shift in await ShiftsAsync(sourceIds, cancellationToken))
        {
            if (shift.TryGet<RegisterShiftPayroll>(out var payroll) && payroll is not null && payroll.ConsumerIds.Remove(consumerId))
            {
                shift.Put(payroll);
                await _shifts.UpdateAsync(shift, cancellationToken);
            }
        }
    }

    private async Task<IReadOnlyList<RegisterShift>> ShiftsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var shifts = new List<RegisterShift>();

        foreach (var id in ids.Distinct(StringComparer.Ordinal))
        {
            if (await _shifts.FindByIdAsync(id, cancellationToken) is { } shift)
            {
                shifts.Add(shift);
            }
        }

        return shifts;
    }

    // The pay code with the code, else the first enabled tips code of the class.
    private static PayCode Code(IEnumerable<PayCode> codes, string code, PayCodeClass payCodeClass)
        => codes.FirstOrDefault(candidate => candidate.Enabled && string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? codes.FirstOrDefault(candidate => candidate.Enabled && candidate.Kind == PayCodeKinds.Tips && candidate.Class == payCodeClass);
}
