using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// The editor of a transaction workflow event: the sources and reference types whose transactions start the workflow.
/// </summary>
/// <typeparam name="TActivity">The event type.</typeparam>
public sealed class TransactionEventActivityDisplayDriver<TActivity> : ActivityDisplayDriver<TActivity, TransactionEventActivityViewModel>
    where TActivity : TransactionEventActivity
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionEventActivityDisplayDriver{TActivity}"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionEventActivityDisplayDriver(IStringLocalizer<TransactionEventActivityDisplayDriver<TActivity>> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(TActivity activity, BuildEditorContext context)
        => Initialize<TransactionEventActivityViewModel>($"{ActivityName}_Fields_Edit", model => EditActivityAsync(activity, model))
            .Location(l => l.Zone("Content", "1").Card(S["Settings"], "10"));

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
