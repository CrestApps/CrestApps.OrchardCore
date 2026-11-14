using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Payroll.Core.Calculation;

/// <summary>
/// Runs the calculation steps in order. A step registered with the order of an earlier one replaces it. A gross-up
/// repeats the steps, raising the earning until net pay reaches the target.
/// </summary>
public sealed class PayCalculator : IPayCalculator
{
    private const int MaximumGrossUpRounds = 60;

    private readonly IReadOnlyList<IPayCalculationStep> _steps;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayCalculator"/> class.
    /// </summary>
    /// <param name="steps">The steps; of steps with the same order, the last registered runs.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public PayCalculator(IEnumerable<IPayCalculationStep> steps, IStringLocalizer<PayCalculator> stringLocalizer)
    {
        S = stringLocalizer;
        _steps = [.. steps
            .GroupBy(step => step.Order)
            .Select(group => group.Last())
            .OrderBy(step => step.Order)];
    }

    /// <inheritdoc />
    public async Task CalculateAsync(PayCalculationContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.GrossUp is not { } grossUp)
        {
            await RunAsync(context, cancellationToken);

            return;
        }

        // Start from the target: taxes and deductions then lower net pay, and each round adds back what is missing.
        grossUp.Amount = Math.Max(0, grossUp.TargetNet);

        for (var round = 0; round < MaximumGrossUpRounds; round++)
        {
            await RunAsync(context, cancellationToken);

            var difference = grossUp.TargetNet - context.Net;

            if (Math.Abs(difference) < 0.01m)
            {
                return;
            }

            grossUp.Amount = context.Round(Math.Max(0, grossUp.Amount + difference));
        }

        context.Warnings.Add(S["The gross-up did not reach the target net pay of {0}; it stopped at {1}.", grossUp.TargetNet, context.Net]);
    }

    private async Task RunAsync(PayCalculationContext context, CancellationToken cancellationToken)
    {
        context.Reset();

        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await step.ExecuteAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Gets the pay periods in a year of a frequency.
    /// </summary>
    /// <param name="frequency">The frequency.</param>
    /// <returns>The periods.</returns>
    public static int PeriodsPerYear(PayFrequency frequency)
        => frequency switch
        {
            PayFrequency.Weekly => 52,
            PayFrequency.Biweekly => 26,
            PayFrequency.SemiMonthly => 24,
            PayFrequency.Monthly => 12,
            PayFrequency.FourWeekly => 13,
            PayFrequency.Quarterly => 4,
            PayFrequency.Yearly => 1,
            _ => 26,
        };
}
