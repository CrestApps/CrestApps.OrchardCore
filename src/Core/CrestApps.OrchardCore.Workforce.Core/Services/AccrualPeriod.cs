namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// One accrual period, its first and last day.
/// </summary>
/// <param name="Start">The first day.</param>
/// <param name="End">The last day.</param>
internal readonly record struct AccrualPeriod(DateOnly Start, DateOnly End);
