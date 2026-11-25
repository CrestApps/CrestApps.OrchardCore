using System.Reflection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayrollTaskDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public PayrollTaskDisplayDriver(IStringLocalizer<PayrollTaskDisplayDriver<TActivity>> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
        => Initialize<PayrollTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivity(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));

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
