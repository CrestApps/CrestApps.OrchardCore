using System.Reflection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Calendars.Workflows;

/// <summary>
/// The editor of a calendar workflow task: copies the task's text fields to and from <see cref="CalendarTaskViewModel"/>.
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public sealed class CalendarTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, CalendarTaskViewModel>
    where TActivity : CalendarTaskActivity<TActivity>, ITask
{
    private static readonly PropertyInfo[] _modelProperties = typeof(CalendarTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarTaskDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CalendarTaskDisplayDriver(IStringLocalizer<CalendarTaskDisplayDriver<TActivity>> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
        => Initialize<CalendarTaskViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));

    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, CalendarTaskViewModel model)
    {
        foreach (var property in _modelProperties)
        {
            var source = typeof(TActivity).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);

            if (source is not null && source.PropertyType == typeof(string))
            {
                property.SetValue(model, source.GetValue(activity));
            }
        }
    }

    /// <inheritdoc />
    protected override void UpdateActivity(CalendarTaskViewModel model, TActivity activity)
    {
        foreach (var property in _modelProperties)
        {
            var target = typeof(TActivity).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance);

            if (target is not null && target.PropertyType == typeof(string) && target.CanWrite)
            {
                var value = property.GetValue(model) as string;

                target.SetValue(activity, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
            }
        }
    }
}
