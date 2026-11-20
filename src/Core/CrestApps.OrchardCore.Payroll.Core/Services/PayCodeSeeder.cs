using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Adds the usual pay codes to a site that has none.
/// </summary>
public sealed class PayCodeSeeder
{
    private readonly IPayCodeManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodeSeeder"/> class.
    /// </summary>
    /// <param name="manager">The pay codes.</param>
    public PayCodeSeeder(IPayCodeManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Adds the usual pay codes when there is none.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if ((await _manager.GetAllAsync(cancellationToken)).Any())
        {
            return;
        }

        foreach (var (code, name, kind, multiplier) in Defaults)
        {
            var item = await _manager.NewAsync(cancellationToken: cancellationToken);

            item.Code = code;
            item.Name = name;
            item.Class = kind == PayCodeKinds.Unpaid ? PayCodeClass.Memo : PayCodeClass.Earning;
            item.Kind = kind;
            item.Multiplier = multiplier;
            await _manager.CreateAsync(item, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the usual pay codes.
    /// </summary>
    public static IReadOnlyList<(string Code, string Name, string Kind, decimal Multiplier)> Defaults { get; } =
    [
        ("REG", "Regular", PayCodeKinds.Regular, 1m),
        ("OT", "Overtime", PayCodeKinds.Overtime, 1.5m),
        ("DT", "Double time", PayCodeKinds.DoubleTime, 2m),
        ("PTO", "Paid time off", PayCodeKinds.PaidTimeOff, 1m),
        ("PP", "Predictability pay", PayCodeKinds.PredictabilityPay, 1m),
        ("HOL", "Holiday", PayCodeKinds.Holiday, 1.5m),
        ("WKND", "Weekend", PayCodeKinds.Weekend, 1.5m),
        ("UNPAID", "Unpaid", PayCodeKinds.Unpaid, 0m),
    ];
}
