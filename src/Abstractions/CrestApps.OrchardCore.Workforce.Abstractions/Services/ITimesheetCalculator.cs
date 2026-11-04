namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Works a timesheet out: pairs punches into spans, applies rounding and grace, splits at midnight in the worker's
/// time, classifies hours into pay codes and overtime, adds time off and premiums, and finds attendance exceptions
/// against the schedule. It reads nothing and saves nothing.
/// </summary>
public interface ITimesheetCalculator
{
    /// <summary>
    /// Works a timesheet out.
    /// </summary>
    /// <param name="input">What to work it out from.</param>
    /// <returns>The lines, spans and exceptions.</returns>
    TimesheetCalculation Calculate(TimesheetCalculationInput input);
}
