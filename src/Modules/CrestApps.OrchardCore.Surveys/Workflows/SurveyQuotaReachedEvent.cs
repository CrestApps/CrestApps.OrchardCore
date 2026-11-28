using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Starts when a quota of a survey reaches its limit, optionally only some quotas.
/// </summary>
public sealed class SurveyQuotaReachedEvent : SurveyEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyQuotaReachedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveyQuotaReachedEvent(IStringLocalizer<SurveyQuotaReachedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc />
    public override string Name => nameof(SurveyQuotaReachedEvent);

    /// <inheritdoc />
    public override LocalizedString DisplayText => S["Survey Quota Reached"];

    /// <summary>
    /// Gets or sets the names of the quotas that start the workflow; empty for every quota.
    /// </summary>
    public IList<string> QuotaNames
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc />
    protected override bool Matches(WorkflowExecutionContext workflowContext)
        => SurveyResponseEventActivity.Allows(QuotaNames, workflowContext.Input, SurveyWorkflowInput.QuotaName);
}
