using CrestApps.Core;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Services;

/// <summary>
/// Adds the pay codes payroll needs beyond the time clock's: salary, bonus, commission, tips, a reimbursement and a
/// loan repayment, each with its Payroll settings. A code already there is left as it is.
/// </summary>
public sealed class PayrollPayCodeSeeder
{
    private readonly IPayCodeManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollPayCodeSeeder"/> class.
    /// </summary>
    /// <param name="manager">The pay codes.</param>
    public PayrollPayCodeSeeder(IPayCodeManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Adds each payroll pay code whose code is not used yet.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The codes added.</returns>
    public async Task<IReadOnlyList<string>> SeedAsync(CancellationToken cancellationToken = default)
    {
        var existing = (await _manager.GetAllAsync(cancellationToken)).Select(code => code.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<string>();

        foreach (var seed in Defaults)
        {
            if (!existing.Add(seed.Code))
            {
                continue;
            }

            var code = await _manager.NewAsync(cancellationToken: cancellationToken);

            code.Code = seed.Code;
            code.Name = seed.Name;
            code.Class = seed.Class;
            code.Kind = seed.Kind;
            code.Multiplier = 1;

            var settings = PayrollPayCodeSettingsExtensions.DefaultsFor(code);

            seed.Configure?.Invoke(settings);
            code.Put(settings);
            await _manager.CreateAsync(code, cancellationToken);
            added.Add(seed.Code);
        }

        return added;
    }

    /// <summary>
    /// Gets the payroll pay codes.
    /// </summary>
    public static IReadOnlyList<PayCodeSeed> Defaults { get; } =
    [
        new("SAL", "Salary", PayCodeClass.Earning, PayCodeKinds.Salary, settings => settings.CountsAsHoursWorked = true),
        new("BONUS", "Bonus", PayCodeClass.Earning, PayCodeKinds.Bonus, null),
        new("COMM", "Commission", PayCodeClass.Earning, PayCodeKinds.Commission, null),
        new("TIPS", "Tips", PayCodeClass.Earning, PayCodeKinds.Tips, null),
        new("CTIPS", "Cash tips reported", PayCodeClass.ImputedIncome, PayCodeKinds.Tips, null),
        new("TIPMU", "Tip credit make-up", PayCodeClass.Earning, PayCodeKinds.MinimumWageMakeup, settings =>
        {
            settings.CountsInRegularRate = true;
            settings.CountsAsHoursWorked = false;
        }),
        new("REIMB", "Expense reimbursement", PayCodeClass.Reimbursement, PayCodeKinds.Reimbursement, settings => settings.InRetroPay = false),
        new("LOAN", "Loan repayment", PayCodeClass.Deduction, PayCodeKinds.Loan, settings =>
        {
            settings.Priority = 200;
            settings.Arrears = PayCodeArrearsRule.CarryForward;
            settings.InRetroPay = false;
        }),
    ];
}

/// <summary>
/// A pay code the payroll seeder adds.
/// </summary>
/// <param name="Code">The code.</param>
/// <param name="Name">The name.</param>
/// <param name="Class">The class.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Configure">Changes to the defaults of its class and kind, or <see langword="null"/>.</param>
public sealed record PayCodeSeed(string Code, string Name, PayCodeClass Class, string Kind, Action<PayrollPayCodeSettings> Configure);

/// <summary>
/// Keeps a pay code that another code takes a percent of from being deleted.
/// </summary>
public sealed class PayCodeBaseUsage : IPaySetupUsage
{
    private readonly IServiceProvider _services;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCodeBaseUsage"/> class.
    /// </summary>
    /// <param name="services">The services, to read the pay codes when asked (the pay code store asks this usage, so
    /// it can't be a dependency).</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayCodeBaseUsage(IServiceProvider services, IStringLocalizer<PayCodeBaseUsage> stringLocalizer)
    {
        _services = services;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetUsagesAsync(PaySchedule schedule, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUsagesAsync(PayCode code, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(code);

        return [.. (await _services.GetRequiredService<IPayCodeManager>().GetAllAsync(cancellationToken))
            .Where(other => other.ItemId != code.ItemId
                && other.TryGet<PayrollPayCodeSettings>(out var settings)
                && settings?.BaseCodeIds.Contains(code.ItemId) == true)
            .Select(other => S["the percent of pay code {0}", other.Code].Value)];
    }
}
