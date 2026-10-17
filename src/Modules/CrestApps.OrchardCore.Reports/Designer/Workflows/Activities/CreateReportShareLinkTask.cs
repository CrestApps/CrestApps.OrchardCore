using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.DataPipelines.SharedLinks;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;

/// <summary>
/// Shares a report with users of the site through a shared link of Orchard Core, emails the link to them when asked to,
/// and writes its address to the workflow output, so a following task can send it another way. The link works for its
/// recipients, once signed in, until it expires, is used as many times as allowed, or is revoked on the report's
/// Sharing tab or under Tools &gt; Shared Links.
/// </summary>
public sealed class CreateReportShareLinkTask : ReportTaskActivity<CreateReportShareLinkTask>
{
    /// <summary>
    /// The output name used when none is set.
    /// </summary>
    public const string DefaultOutputName = "ReportShareLink";

    private readonly ReportWorkflowService _reports;
    private readonly ISharedLinkManager _sharedLinks;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateReportShareLinkTask"/> class.
    /// </summary>
    /// <param name="reports">The report workflow service.</param>
    /// <param name="sharedLinks">The shared links of Orchard Core.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreateReportShareLinkTask(
        ReportWorkflowService reports,
        ISharedLinkManager sharedLinks,
        ILogger<CreateReportShareLinkTask> logger,
        IStringLocalizer<CreateReportShareLinkTask> stringLocalizer)
        : base(stringLocalizer)
    {
        _reports = reports;
        _sharedLinks = sharedLinks;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Create Report Share Link"];

    /// <summary>
    /// Gets or sets the user names or emails of the users of the site the link is for, separated by commas, with Liquid.
    /// </summary>
    public string Recipients
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a note about what the link is for, with Liquid, sent with the email and shown on the report's
    /// Sharing tab.
    /// </summary>
    public string Note
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the number of days the link works.
    /// </summary>
    public int ExpiresAfterDays
    {
        get => GetProperty(() => 7);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the most times the link may be used, or <c>0</c> for no limit.
    /// </summary>
    public int MaxUses
    {
        get => GetProperty<int>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the link also allows exporting.
    /// </summary>
    public bool AllowExport
    {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the link is emailed to its recipients.
    /// </summary>
    public bool NotifyByEmail
    {
        get => GetProperty(() => true);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        return [new Outcome(S["Done"]), new Outcome(S["Failed"])];
    }

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var design = await _reports.FindReportAsync(this, workflowContext);

        if (design is null)
        {
            _logger.LogWarning("The Create Report Share Link task of workflow '{WorkflowId}' found no report.", workflowContext.WorkflowId);

            return Outcome("Failed");
        }

        var recipients = (await _reports.EvaluateAsync(Recipients, workflowContext) ?? string.Empty)
            .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var note = (await _reports.EvaluateAsync(Note, workflowContext))?.Trim();
        var request = new SharedLinkRequest
        {
            Kind = ReportsConstants.SharedLinkKind,
            ResourceId = design.ItemId,
            Title = design.DisplayText,
            SourceName = design.DisplayText,
            Recipients = recipients,
            Lifetime = TimeSpan.FromDays(Math.Max(1, ExpiresAfterDays)),
            MaxUses = Math.Max(0, MaxUses),
            NotifyByEmail = NotifyByEmail,
            Message = note,
        };

        request.Properties[ReportSharedLinkHandler.AllowExportProperty] = AllowExport ? "true" : "false";

        if (!string.IsNullOrEmpty(note))
        {
            request.Properties[ReportSharedLinkHandler.NoteProperty] = note;
        }

        SharedLinkResult result;

        try
        {
            result = await _sharedLinks.CreateAsync(request);
        }
        catch (InvalidOperationException exception)
        {
            // A recipient is not a user of the site, there is no recipient, or emailing is asked for while the Email feature
            // is off.
            _logger.LogWarning(exception, "The Create Report Share Link task of workflow '{WorkflowId}' could not share report '{ReportId}'.", workflowContext.WorkflowId, design.ItemId);

            return Outcome("Failed");
        }

        var output = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["ReportId"] = design.ItemId,
            ["Title"] = design.DisplayText,
            ["Url"] = result.Url,
            ["LinkId"] = result.Link.LinkId,
            ["ExpiresUtc"] = result.Link.ExpiresUtc,
            ["Recipients"] = result.Link.RecipientNames.ToArray(),
            ["Notified"] = result.Notified.ToArray(),
        };

        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? DefaultOutputName : OutputName.Trim()] = output;
        workflowContext.LastResult = result.Url;

        return Outcome("Done");
    }
}
