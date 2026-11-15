using CrestApps.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Payroll;

/// <summary>
/// The pay runs and exports that took an order's commission, stored on the order.
/// </summary>
public sealed class OrderCommissionPayroll
{
    /// <summary>
    /// Gets or sets the ids of the pay runs or exports that took it.
    /// </summary>
    public List<string> ConsumerIds { get; set; } = [];
}

/// <summary>
/// Hands sales commissions to payroll: each paid order a staff member made (at a register or as a draft, its
/// <see cref="Order.StaffUserId"/>) on a day of the period in the site's time zone earns them their commission rate
/// (Staff compensation) of what it came to after discounts and refunds, on the Commission pay code. A pay run or an export
/// that takes it marks the order, so a commission is paid once; a voided run gives it back.
/// </summary>
internal sealed class OrderCommissionPayInputSource : IPayInputSource
{
    /// <summary>
    /// The kind of record a commission comes from.
    /// </summary>
    public const string Kind = "Order";

    private readonly OrderReportReader _orders;
    private readonly IOrderStore _orderStore;
    private readonly IStaffMemberManager _staff;
    private readonly IPayCodeManager _codes;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;

    public OrderCommissionPayInputSource(OrderReportReader orders, IOrderStore orderStore, IStaffMemberManager staff, IPayCodeManager codes, ILocalClock localClock, IClock clock)
    {
        _clock = clock;
        _orders = orders;
        _orderStore = orderStore;
        _staff = staff;
        _codes = codes;
        _localClock = localClock;
    }

    public string SourceKind => Kind;

    public async Task<IReadOnlyList<PayInput>> GetInputsAsync(PayInputQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var code = (await _codes.GetAllAsync(cancellationToken))
            .Where(candidate => candidate.Enabled && candidate.Kind == PayCodeKinds.Commission && candidate.Class == PayCodeClass.Earning)
            .OrderBy(candidate => !string.Equals(candidate.Code, "COMM", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        if (code is null)
        {
            return [];
        }

        // The site's zone is within a day of UTC: read a wider window, then keep the orders paid on a day of the period.
        var fromUtc = (query.PeriodStart ?? DateOnly.FromDateTime(_clock.UtcNow.AddYears(-1))).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-2);
        var toUtc = (query.PeriodEnd ?? DateOnly.FromDateTime(_clock.UtcNow)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(2);
        var sales = new List<(Order Order, DateOnly Day)>();

        await foreach (var order in _orders.ReadPaidAsync(fromUtc, toUtc, cancellationToken))
        {
            if (order.Status != OrderStatus.Paid || string.IsNullOrEmpty(order.StaffUserId) || order.PaidUtc is not { } paid)
            {
                continue;
            }

            var day = DateOnly.FromDateTime((await _localClock.ConvertToLocalAsync(DateTime.SpecifyKind(paid, DateTimeKind.Utc))).DateTime);

            if ((query.PeriodStart.HasValue && day < query.PeriodStart.Value) || (query.PeriodEnd.HasValue && day > query.PeriodEnd.Value) || (!query.IncludeConsumed && IsConsumed(order)))
            {
                continue;
            }

            sales.Add((order, day));
        }

        if (sales.Count == 0)
        {
            return [];
        }

        var people = (await _staff.GetByUserIdsAsync(sales.Select(sale => sale.Order.StaffUserId).Distinct(StringComparer.Ordinal), cancellationToken))
            .Where(member => !string.IsNullOrEmpty(member.UserId))
            .GroupBy(member => member.UserId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var inputs = new List<PayInput>();

        foreach (var (order, day) in sales.OrderBy(sale => sale.Day))
        {
            if (people.GetValueOrDefault(order.StaffUserId) is not { } member
                || (query.StaffMemberIds is { Count: > 0 } && !query.StaffMemberIds.Contains(member.ItemId))
                || !member.TryGet<StaffPayRates>(out var pay) || pay?.CommissionRate is not > 0m)
            {
                continue;
            }

            var sold = Math.Max(0m, order.Subtotal - order.DiscountTotal - order.RefundedAmount);
            var commission = Math.Round(sold * pay.CommissionRate.Value / 100m, 2, MidpointRounding.AwayFromZero);

            if (commission <= 0m)
            {
                continue;
            }

            inputs.Add(new PayInput
            {
                SourceKind = Kind,
                SourceId = order.ItemId,
                StaffMemberId = member.ItemId,
                StaffMemberName = member.DisplayName,
                EmployeeNumber = member.EmployeeNumber,
                PayrollId = member.ExternalId ?? member.EmployeeNumber,
                PayScheduleId = query.PayScheduleId,
                PeriodStart = query.PeriodStart ?? day,
                PeriodEnd = query.PeriodEnd ?? day,
                PayCodeId = code.ItemId,
                Date = day,
                Amount = commission,
                Consumed = IsConsumed(order),
            });
        }

        return inputs;
    }

    public async Task MarkConsumedAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        foreach (var id in sourceIds.Distinct(StringComparer.Ordinal))
        {
            if (await _orderStore.FindByIdAsync(id, cancellationToken) is not { } order)
            {
                continue;
            }

            var payroll = order.TryGet<OrderCommissionPayroll>(out var stored) && stored is not null ? stored : new OrderCommissionPayroll();

            if (!payroll.ConsumerIds.Contains(consumerId))
            {
                payroll.ConsumerIds.Add(consumerId);
                order.Put(payroll);
                await _orderStore.UpdateAsync(order, cancellationToken);
            }
        }
    }

    public async Task ReleaseAsync(string consumerId, IReadOnlyCollection<string> sourceIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(consumerId);
        ArgumentNullException.ThrowIfNull(sourceIds);

        foreach (var id in sourceIds.Distinct(StringComparer.Ordinal))
        {
            if (await _orderStore.FindByIdAsync(id, cancellationToken) is { } order
                && order.TryGet<OrderCommissionPayroll>(out var payroll) && payroll is not null && payroll.ConsumerIds.Remove(consumerId))
            {
                order.Put(payroll);
                await _orderStore.UpdateAsync(order, cancellationToken);
            }
        }
    }

    private static bool IsConsumed(Order order)
        => order.TryGet<OrderCommissionPayroll>(out var payroll) && payroll?.ConsumerIds.Count > 0;
}
