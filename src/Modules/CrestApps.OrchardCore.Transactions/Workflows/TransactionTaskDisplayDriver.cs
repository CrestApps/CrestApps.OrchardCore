using System.Reflection;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// The editor of a transaction workflow task: copies the task's text fields to and from
/// <see cref="TransactionTaskViewModel"/>.
/// </summary>
/// <typeparam name="TActivity">The task type.</typeparam>
public sealed class TransactionTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, TransactionTaskViewModel>
    where TActivity : TransactionTaskActivity<TActivity>, ITask
{
    private static readonly PropertyInfo[] _modelProperties = typeof(TransactionTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, TransactionTaskViewModel model)
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
    protected override void UpdateActivity(TransactionTaskViewModel model, TActivity activity)
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
