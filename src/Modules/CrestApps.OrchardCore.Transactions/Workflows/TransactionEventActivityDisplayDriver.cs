using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// The editor of a transaction workflow event: the sources and reference types whose transactions start the workflow.
/// </summary>
/// <typeparam name="TActivity">The event type.</typeparam>
public sealed class TransactionEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, TransactionEventActivityViewModel>
    where TActivity : TransactionEventActivity
{
    /// <inheritdoc />
    protected override void EditActivity(TActivity activity, TransactionEventActivityViewModel model)
    {
        model.Sources = activity.Sources;
        model.ReferenceTypes = activity.ReferenceTypes;
    }

    /// <inheritdoc />
    protected override void UpdateActivity(TransactionEventActivityViewModel model, TActivity activity)
    {
        activity.Sources = string.IsNullOrWhiteSpace(model.Sources) ? null : model.Sources.Trim();
        activity.ReferenceTypes = string.IsNullOrWhiteSpace(model.ReferenceTypes) ? null : model.ReferenceTypes.Trim();
    }
}
