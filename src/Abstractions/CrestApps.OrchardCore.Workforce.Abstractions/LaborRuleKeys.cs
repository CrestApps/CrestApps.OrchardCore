namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// The keys of the built-in labor rules.
/// </summary>
public static class LaborRuleKeys
{
    /// <summary>
    /// The most paid hours in a day.
    /// </summary>
    public const string MaxHoursPerDay = "MaxHoursPerDay";

    /// <summary>
    /// The most paid hours in a week, Monday to Sunday.
    /// </summary>
    public const string MaxHoursPerWeek = "MaxHoursPerWeek";

    /// <summary>
    /// The fewest paid hours in a week.
    /// </summary>
    public const string MinHoursPerWeek = "MinHoursPerWeek";

    /// <summary>
    /// The fewest hours between shifts.
    /// </summary>
    public const string MinRest = "MinRest";

    /// <summary>
    /// The most days worked in a row.
    /// </summary>
    public const string MaxConsecutiveDays = "MaxConsecutiveDays";

    /// <summary>
    /// The fewest days off in a week.
    /// </summary>
    public const string DaysOffPerWeek = "DaysOffPerWeek";

    /// <summary>
    /// The longest shift.
    /// </summary>
    public const string MaxShiftLength = "MaxShiftLength";

    /// <summary>
    /// The shortest shift.
    /// </summary>
    public const string MinShiftLength = "MinShiftLength";

    /// <summary>
    /// The breaks a long shift needs.
    /// </summary>
    public const string Breaks = "Breaks";

    /// <summary>
    /// The limits on young workers.
    /// </summary>
    public const string MinorLimits = "MinorLimits";

    /// <summary>
    /// The skills a shift needs, held on its day.
    /// </summary>
    public const string SkillsRequired = "SkillsRequired";

    /// <summary>
    /// No shift during approved time off.
    /// </summary>
    public const string NoWorkDuringTimeOff = "NoWorkDuringTimeOff";

    /// <summary>
    /// The notice a change to a published shift needs.
    /// </summary>
    public const string ScheduleNotice = "ScheduleNotice";

    /// <summary>
    /// The hours after which time is overtime.
    /// </summary>
    public const string Overtime = "Overtime";

    /// <summary>
    /// The premium owed for a late change to a published shift.
    /// </summary>
    public const string PredictabilityPay = "PredictabilityPay";
}
