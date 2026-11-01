using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Adds the usual pay codes when the time clock is turned on and there are none.
/// </summary>
internal sealed class PayCodeSeeder
{
    private readonly IPayCodeManager _manager;

    public PayCodeSeeder(IPayCodeManager manager)
    {
        _manager = manager;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if ((await _manager.GetAllAsync(cancellationToken)).Any())
        {
            return;
        }

        foreach (var (code, name, kind, multiplier) in new[]
        {
            ("REG", "Regular", PayCodeKind.Regular, 1m),
            ("OT", "Overtime", PayCodeKind.Overtime, 1.5m),
            ("DT", "Double time", PayCodeKind.DoubleTime, 2m),
            ("PTO", "Paid time off", PayCodeKind.PaidTimeOff, 1m),
            ("PP", "Predictability pay", PayCodeKind.PredictabilityPay, 1m),
            ("HOL", "Holiday", PayCodeKind.Holiday, 1m),
            ("UNPAID", "Unpaid", PayCodeKind.Unpaid, 0m),
        })
        {
            var item = await _manager.NewAsync(cancellationToken: cancellationToken);

            item.Code = code;
            item.Name = name;
            item.Kind = kind;
            item.Multiplier = multiplier;
            await _manager.CreateAsync(item, cancellationToken);
        }
    }
}
