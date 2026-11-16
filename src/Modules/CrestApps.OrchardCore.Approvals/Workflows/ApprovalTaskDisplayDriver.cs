using System.Reflection;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// The editor of an Approvals task: copies the task's Liquid fields to and from <see cref="ApprovalTaskViewModel"/>.
/// </summary>
/// <typeparam name="TActivity">The task.</typeparam>
public sealed class ApprovalTaskDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, ApprovalTaskViewModel>
    where TActivity : TaskActivity
{
    private static readonly PropertyInfo[] _modelProperties = typeof(ApprovalTaskViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, ApprovalTaskViewModel model)
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
    protected override void UpdateActivity(ApprovalTaskViewModel model, TActivity activity)
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
/// The Liquid fields of the Approvals tasks; each task uses some of them.
/// </summary>
public class ApprovalTaskViewModel
{
    /// <summary>
    /// Gets or sets the policy.
    /// </summary>
    public string Policy { get; set; }

    /// <summary>
    /// Gets or sets the subject's kind.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the subject's id.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the access scope.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets what is asked.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the subject's page.
    /// </summary>
    public string SubjectUrl { get; set; }

    /// <summary>
    /// Gets or sets the amount (Liquid).
    /// </summary>
    public string Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency (Liquid).
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the request id.
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    /// Gets or sets the note.
    /// </summary>
    public string Note { get; set; }
}
