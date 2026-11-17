using System.Reflection;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Payroll.Workflows;

/// <summary>
/// The editor of Payroll's tasks: copies each text setting the task has between it and the form.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public sealed class PayrollTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, PayrollTaskViewModel>
    where TActivity : TaskActivity
{
    private static readonly PropertyInfo[] _modelProperties = typeof(PayrollTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, PayrollTaskViewModel model)
    {
        foreach (var property in _modelProperties)
        {
            if (typeof(TActivity).GetProperty(property.Name, typeof(string)) is { } source)
            {
                property.SetValue(model, source.GetValue(activity));
            }
        }
    }

    /// <inheritdoc />
    protected override void UpdateActivity(PayrollTaskViewModel model, TActivity activity)
    {
        foreach (var property in _modelProperties)
        {
            if (typeof(TActivity).GetProperty(property.Name, typeof(string)) is { CanWrite: true } target)
            {
                target.SetValue(activity, property.GetValue(model));
            }
        }
    }
}

/// <summary>
/// The settings of Payroll's tasks.
/// </summary>
public class PayrollTaskViewModel
{
    public string EmployerId { get; set; }

    public string PayScheduleId { get; set; }

    public string RunType { get; set; }

    public string PeriodStart { get; set; }

    public string PeriodEnd { get; set; }

    public string PayDate { get; set; }

    public string PayRunId { get; set; }

    public string EmployeeNumber { get; set; }

    public string PayCode { get; set; }

    public string Hours { get; set; }

    public string Amount { get; set; }

    public string Note { get; set; }

    public string IncludeTotals { get; set; }
}
