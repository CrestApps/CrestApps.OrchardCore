using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Workflows.Models;

/// <summary>
/// A workflow task that converts a lead, for automations the subject flow does not cover, such as converting a lead
/// when a web form or an outside system says it qualified. Like the Convert Lead subject action, it merges into an
/// existing contact only when exactly one shares the lead's number or email.
/// </summary>
public sealed class ConvertLeadTask : TaskActivity<ConvertLeadTask>
{
    /// <summary>
    /// The expression the task starts with: the content item the workflow was started for.
    /// </summary>
    public const string DefaultLeadExpression = "{{ Workflow.Input.ContentItem.ContentItemId }}";

    private readonly ILeadConversionService _conversionService;
    private readonly LeadMatchFinder _matchFinder;
    private readonly IContentManager _contentManager;
    private readonly IWorkflowExpressionEvaluator _expressionEvaluator;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConvertLeadTask"/> class.
    /// </summary>
    /// <param name="conversionService">The lead conversion service.</param>
    /// <param name="matchFinder">Finds the contacts that share the lead's number or email.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="expressionEvaluator">The workflow expression evaluator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor, for the user who converts.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ConvertLeadTask(
        ILeadConversionService conversionService,
        LeadMatchFinder matchFinder,
        IContentManager contentManager,
        IWorkflowExpressionEvaluator expressionEvaluator,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ConvertLeadTask> logger,
        IStringLocalizer<ConvertLeadTask> stringLocalizer)
    {
        _conversionService = conversionService;
        _matchFinder = matchFinder;
        _contentManager = contentManager;
        _expressionEvaluator = expressionEvaluator;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Convert Lead"];

    /// <inheritdoc/>
    public override LocalizedString Category => S["Omnichannel CRM"];

    /// <summary>
    /// Gets or sets the Liquid expression that resolves the content item id of the lead.
    /// </summary>
    public string LeadContentItemId
    {
        get => GetProperty(() => DefaultLeadExpression);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets how the contact gets an account.
    /// </summary>
    public LeadConversionAccountMode AccountMode
    {
        get => GetProperty(() => LeadConversionAccountMode.Automatic);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether the conversion also creates an opportunity.
    /// </summary>
    public bool CreateOpportunity
    {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the opportunity type; empty uses the lead type's default.
    /// </summary>
    public string OpportunityContentType
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets what happens to the lead's other open activities.
    /// </summary>
    public LeadOpenActivityMode OpenActivities
    {
        get => GetProperty(() => LeadOpenActivityMode.Move);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        =>
        [
            new Outcome(S["Converted"]),
            new Outcome(S["Failed"]),
        ];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var leadId = (await _expressionEvaluator.EvaluateAsync(new WorkflowExpression<string>(LeadContentItemId), workflowContext, null))?.Trim();

        if (string.IsNullOrEmpty(leadId))
        {
            _logger.LogWarning("The Convert Lead task resolved an empty lead id, so nothing was converted.");

            return Outcome("Failed");
        }

        var lead = await _contentManager.GetAsync(leadId, VersionOptions.Latest);

        if (lead is null || !lead.TryGet<LeadPart>(out _))
        {
            _logger.LogWarning("The Convert Lead task was given an item that is not a lead, so nothing was converted.");

            return Outcome("Failed");
        }

        // An unattended conversion merges into an existing contact only when exactly one shares the lead's number or
        // email; with more than one it cannot tell which, so it creates a new contact rather than guess.
        var matches = await _matchFinder.FindContactsAsync(lead);
        var user = _httpContextAccessor.HttpContext?.User;

        var result = await _conversionService.ConvertAsync(new LeadConversionRequest
        {
            LeadContentItemId = lead.ContentItemId,
            ExistingContactItemId = matches.Count == 1 ? matches[0].ContentItemId : null,
            AccountMode = AccountMode,
            CreateOpportunity = CreateOpportunity,
            OpportunityContentType = string.IsNullOrWhiteSpace(OpportunityContentType) ? null : OpportunityContentType,
            OpenActivities = OpenActivities,
            UserId = user?.FindFirstValue(ClaimTypes.NameIdentifier),
            UserName = user?.Identity?.Name,
        });

        if (!result.Succeeded)
        {
            _logger.LogWarning(
                "The Convert Lead task could not convert a lead: {Errors}",
                string.Join(" ", result.Errors));

            return Outcome("Failed");
        }

        // The next activities can read the contact the lead became.
        workflowContext.LastResult = result.Contact?.ContentItemId;

        return Outcome("Converted");
    }
}
