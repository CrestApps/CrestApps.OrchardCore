using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Learning.Certifications.Workflows;

/// <summary>
/// A certification path event, filtered by certification.
/// </summary>
public abstract class CertificationPathEventActivity : EventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationPathEventActivity"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    protected CertificationPathEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the string localizer.
    /// </summary>
    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Learning"];

    /// <summary>
    /// Gets or sets the certifications that start the workflow; empty for every certification.
    /// </summary>
    public IList<string> CertificationIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => CertificationIds.Count == 0
            || (workflowContext.Input.TryGetValue("CertificationId", out var certificationId) && CertificationIds.Contains(certificationId?.ToString() ?? string.Empty));

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Starts when a holder's renewal opens.
/// </summary>
public sealed class CertificationRenewalOpenedEvent : CertificationPathEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationRenewalOpenedEvent"/> class.
    /// </summary>
    public CertificationRenewalOpenedEvent(IStringLocalizer<CertificationRenewalOpenedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(CertificationRenewalOpenedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Certification Renewal Opened"];
}

/// <summary>
/// Starts when a holder's certificate lapsed without a renewal.
/// </summary>
public sealed class CertificationLapsedEvent : CertificationPathEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificationLapsedEvent"/> class.
    /// </summary>
    public CertificationLapsedEvent(IStringLocalizer<CertificationLapsedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name => nameof(CertificationLapsedEvent);

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Certification Lapsed"];
}
