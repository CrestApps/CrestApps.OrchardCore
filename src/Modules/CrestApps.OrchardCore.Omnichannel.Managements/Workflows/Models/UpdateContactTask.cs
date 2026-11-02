using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Settings;
using CrestApps.Core.Support;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;

/// <summary>
/// A workflow task that changes fields on a contact or lead without checking the record's required fields, for
/// automations that only need to record something on it, such as marking a customer as already texted.
/// </summary>
/// <remarks>
/// Orchard Core's Update Content Item task validates the whole record first, and records loaded from a file often
/// lack a field the type requires, such as a last name. Validation then fails, and the task cancels the workflow's
/// whole database session: anything the workflow did before it, a text it sent included, is never recorded, and the
/// field is never set, so the next run does the same thing again. Only the fields this task names are changed, so
/// nothing it writes can make the record less valid than it was.
/// </remarks>
public sealed class UpdateContactTask : TaskActivity<UpdateContactTask>
{
    /// <summary>
    /// The expression the task starts with: the content item the workflow was started for.
    /// </summary>
    public const string DefaultContactExpression = "{{ Workflow.Input.ContentItem.ContentItemId }}";

    private static readonly JsonMergeSettings _mergeSettings = new()
    {
        MergeArrayHandling = MergeArrayHandling.Replace,
    };

    private readonly IContentManager _contentManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContactTask"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public UpdateContactTask(
        IContentManager contentManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        ILogger<UpdateContactTask> logger,
        IStringLocalizer<UpdateContactTask> stringLocalizer)
    {
        _contentManager = contentManager;
        _expressionEvaluator = expressionEvaluator;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Update Contact"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Omnichannel CRM"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the content item id of the contact or lead.
    /// </summary>
    public string ContactContentItemId
    {
        get => GetProperty(() => DefaultContactExpression);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the Liquid expression that renders the JSON merged into the record, such as
    /// <c>{ "Customer": { "NoAnswerTextSent": { "Value": true } } }</c>.
    /// </summary>
    public string Properties
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether a record whose latest version is a draft is published once changed. A published record
    /// is always changed in place, so it stays published.
    /// </summary>
    public bool Publish
    {
        get => GetProperty(() => true);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        =>
        [
            new Outcome(S["Done"]),
            new Outcome(S["Failed"]),
        ];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var contactId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(ContactContentItemId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(contactId))
        {
            _logger.LogWarning("The Update Contact task resolved an empty contact id, so nothing was changed.");

            return Outcome("Failed");
        }

        var contact = await _contentManager.GetAsync(contactId, VersionOptions.Latest);

        if (contact is null)
        {
            _logger.LogWarning(
                "The Update Contact task found no record '{ContactContentItemId}', so nothing was changed.",
                contactId.SanitizeLogValue());

            return Outcome("Failed");
        }

        if (string.IsNullOrWhiteSpace(Properties))
        {
            _logger.LogWarning("The Update Contact task has no fields to set, so nothing was changed.");

            return Outcome("Failed");
        }

        // The values are written into JSON, so they are encoded for it, as Orchard Core's own content tasks do.
        var json = await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(Properties), workflowContext, JavaScriptEncoder.Default);

        JsonObject properties;

        try
        {
            properties = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "The Update Contact task could not read the fields to set on record '{ContactContentItemId}' as JSON, so nothing was changed.",
                contactId.SanitizeLogValue());

            return Outcome("Failed");
        }

        if (properties is null)
        {
            _logger.LogWarning(
                "The Update Contact task's fields for record '{ContactContentItemId}' are not a JSON object, so nothing was changed.",
                contactId.SanitizeLogValue());

            return Outcome("Failed");
        }

        contact.Merge(properties, _mergeSettings);

        await _contentManager.UpdateAsync(contact);

        if (Publish && !contact.Published)
        {
            await _contentManager.PublishAsync(contact);
        }

        workflowContext.LastResult = contact.ContentItemId;

        return Outcome("Done");
    }
}
