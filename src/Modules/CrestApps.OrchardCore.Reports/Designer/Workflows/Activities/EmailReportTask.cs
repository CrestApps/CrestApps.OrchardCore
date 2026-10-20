using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Email;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;

/// <summary>
/// Runs a report with its owner's access and emails it: attached as a file, such as CSV or Excel, and, when chosen,
/// shown in the message as its headline numbers and tables. The recipients, subject and message take Liquid, so a
/// workflow started by a timer can send a report every day.
/// </summary>
public sealed class EmailReportTask : ReportTaskActivity<EmailReportTask>
{
    private readonly ReportWorkflowService _reports;
    private readonly DesignedReportPresenter _presenter;
    private readonly IEmailService _emailService;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly HtmlEncoder _htmlEncoder;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailReportTask"/> class.
    /// </summary>
    /// <param name="reports">The report workflow service.</param>
    /// <param name="presenter">The presenter that exports reports.</param>
    /// <param name="emailService">The email service.</param>
    /// <param name="expressionEvaluator">The evaluator of Liquid expressions.</param>
    /// <param name="htmlEncoder">The HTML encoder of the message.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public EmailReportTask(
        ReportWorkflowService reports,
        DesignedReportPresenter presenter,
        IEmailService emailService,
        IWorkflowExpressionEvaluator expressionEvaluator,
        HtmlEncoder htmlEncoder,
        ILogger<EmailReportTask> logger,
        IStringLocalizer<EmailReportTask> stringLocalizer)
        : base(stringLocalizer)
    {
        _reports = reports;
        _presenter = presenter;
        _emailService = emailService;
        _expressionEvaluator = expressionEvaluator;
        _htmlEncoder = htmlEncoder;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Email Report"];

    /// <summary>
    /// Gets or sets the recipients, separated by commas, with Liquid.
    /// </summary>
    public string To
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the copied recipients, with Liquid.
    /// </summary>
    public string Cc
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the blind copied recipients, with Liquid.
    /// </summary>
    public string Bcc
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the subject, with Liquid; the report title when empty.
    /// </summary>
    public string Subject
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the message, as HTML with Liquid.
    /// </summary>
    public string Body
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the format of the attached file, such as <c>csv</c> or <c>xlsx</c>; empty for no attachment.
    /// </summary>
    public string AttachmentFormat
    {
        get => GetProperty(() => ReportsConstants.CsvExportFormat);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the message shows the report's headline numbers and tables.
    /// </summary>
    public bool IncludeReport
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
        var to = await _reports.EvaluateAsync(To, workflowContext);

        if (design is null || string.IsNullOrEmpty(to))
        {
            _logger.LogWarning("The Email Report task of workflow '{WorkflowId}' needs a report and at least one recipient.", workflowContext.WorkflowId);

            return Outcome("Failed");
        }

        var run = await _reports.RunAsync(design, await _reports.GetFilterValuesAsync(Filters, workflowContext));

        if (run.Document is null)
        {
            _logger.LogWarning("The Email Report task of workflow '{WorkflowId}' could not run report '{ReportId}'.", workflowContext.WorkflowId, design.ItemId);

            return Outcome("Failed");
        }

        var body = string.IsNullOrWhiteSpace(Body)
            ? string.Empty
            : await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Body), workflowContext, _htmlEncoder);

        if (IncludeReport)
        {
            body += ReportWorkflowService.ToHtml(run);
        }

        var message = new MailMessage
        {
            To = to,
            Cc = await _reports.EvaluateAsync(Cc, workflowContext),
            Bcc = await _reports.EvaluateAsync(Bcc, workflowContext),
            Subject = await _reports.EvaluateAsync(Subject, workflowContext) ?? design.DisplayText,
            HtmlBody = body,
        };

        MemoryStream attachment = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(AttachmentFormat))
            {
                var file = _presenter.Export(design, run, AttachmentFormat);

                if (file is null)
                {
                    _logger.LogWarning("The Email Report task of workflow '{WorkflowId}' has an unknown attachment format.", workflowContext.WorkflowId);

                    return Outcome("Failed");
                }

                attachment = new MemoryStream(file.Content);
                message.Attachments.Add(new MailMessageAttachment
                {
                    Filename = file.FileName,
                    Stream = attachment,
                });
            }

            var result = await _emailService.SendAsync(message);

            if (!result.Succeeded)
            {
                _logger.LogWarning(
                    "The Email Report task of workflow '{WorkflowId}' could not send report '{ReportId}': {Errors}",
                    workflowContext.WorkflowId,
                    design.ItemId,
                    string.Join("; ", result.Errors.Select(error => error.Message)));

                return Outcome("Failed");
            }
        }
        finally
        {
            attachment?.Dispose();
        }

        workflowContext.LastResult = design.ItemId;

        return Outcome("Done");
    }
}
