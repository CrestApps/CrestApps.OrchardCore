using CrestApps.OrchardCore.Reports.Designer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Workflows.Activities;

/// <summary>
/// Creates a share link to a report and writes its address to the workflow output, so a following task can send it,
/// such as by email. The link opens the report for anyone who has it, or only for signed-in people, until it expires
/// or is revoked on the report's Sharing tab. Its address starts with the site's base URL, so set one in the general
/// settings for workflows that run without a request, such as on a timer.
/// </summary>
public sealed class CreateReportShareLinkTask : ReportTaskActivity<CreateReportShareLinkTask>
{
    /// <summary>
    /// The output name used when none is set.
    /// </summary>
    public const string DefaultOutputName = "ReportShareLink";

    private readonly ReportWorkflowService _reports;
    private readonly ReportShareLinkService _shareLinks;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateReportShareLinkTask"/> class.
    /// </summary>
    /// <param name="reports">The report workflow service.</param>
    /// <param name="shareLinks">The share link service.</param>
    /// <param name="siteService">The site settings, for the base URL.</param>
    /// <param name="httpContextAccessor">The current request, for the address when the site has no base URL.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CreateReportShareLinkTask(
        ReportWorkflowService reports,
        ReportShareLinkService shareLinks,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IClock clock,
        ILogger<CreateReportShareLinkTask> logger,
        IStringLocalizer<CreateReportShareLinkTask> stringLocalizer)
        : base(stringLocalizer)
    {
        _reports = reports;
        _shareLinks = shareLinks;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Create Report Share Link"];

    /// <summary>
    /// Gets or sets what the link is for, with Liquid, shown on the report's Sharing tab.
    /// </summary>
    public string Note
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the number of days the link works; <c>0</c> for a link that does not expire.
    /// </summary>
    public int ExpiresAfterDays
    {
        get => GetProperty(() => 7);
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
    /// Gets or sets a value indicating whether the link works only for signed-in people.
    /// </summary>
    public bool RequireSignIn
    {
        get => GetProperty<bool>();
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

        DateTime? expiresUtc = ExpiresAfterDays > 0 ? _clock.UtcNow.AddDays(ExpiresAfterDays) : null;
        var note = await _reports.EvaluateAsync(Note, workflowContext);
        var (link, token) = await _shareLinks.CreateAsync(design.ItemId, note, expiresUtc, AllowExport, RequireSignIn, S["Workflow"]);
        var url = await BaseUrlAsync() + "/reports/shared/" + Uri.EscapeDataString(token);

        var output = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["ReportId"] = design.ItemId,
            ["Title"] = design.DisplayText,
            ["Url"] = url,
            ["LinkId"] = link.ItemId,
            ["ExpiresUtc"] = link.ExpiresUtc,
        };

        workflowContext.Output[string.IsNullOrWhiteSpace(OutputName) ? DefaultOutputName : OutputName.Trim()] = output;
        workflowContext.LastResult = url;

        return Outcome("Done");
    }

    // The site's base URL when it has one, otherwise the address of the current request, with the tenant's prefix.
    private async Task<string> BaseUrlAsync()
    {
        var baseUrl = (await _siteService.GetSiteSettingsAsync())?.BaseUrl;

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl.TrimEnd('/');
        }

        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null || !request.Host.HasValue
            ? request?.PathBase.Value?.TrimEnd('/') ?? string.Empty
            : $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
    }
}
