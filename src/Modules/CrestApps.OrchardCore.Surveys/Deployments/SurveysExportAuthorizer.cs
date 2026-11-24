using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Surveys.Deployments;

/// <summary>
/// Tells whether the person running an export may export responses and secrets.
/// </summary>
internal sealed class SurveysExportAuthorizer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysExportAuthorizer"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="logger">The logger.</param>
    public SurveysExportAuthorizer(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ILogger<SurveysExportAuthorizer> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets whether the current user holds the Export survey responses permission. An export without a signed-in user
    /// may not.
    /// </summary>
    /// <returns><see langword="true"/> when allowed.</returns>
    public async Task<bool> CanExportResponsesAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            && await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ExportSurveyResponses);
    }

    /// <summary>
    /// Gets the id of the current user, or <see langword="null"/>.
    /// </summary>
    /// <returns>The user id.</returns>
    public string CurrentUserId()
        => _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// The property a refused export step carries, saying why it holds no record.
    /// </summary>
    public const string RefusedProperty = "refused";

    /// <summary>
    /// Records an export of one kind of record in the audit trail, with how many were written.
    /// </summary>
    /// <param name="auditTrail">The audit trail.</param>
    /// <param name="eventName">The event.</param>
    /// <param name="count">How many records were written.</param>
    /// <returns>A task.</returns>
    public Task ExportedAsync(ISurveyAuditTrail auditTrail, string eventName, int count)
        => ExportedAsync(auditTrail, eventName, count, null);

    /// <summary>
    /// Records an export in the audit trail with what it exported, such as its surveys and days.
    /// </summary>
    /// <param name="auditTrail">The audit trail.</param>
    /// <param name="eventName">The event.</param>
    /// <param name="count">How many records were written.</param>
    /// <param name="scope">What was exported, or <see langword="null"/>.</param>
    /// <returns>A task.</returns>
    public Task ExportedAsync(ISurveyAuditTrail auditTrail, string eventName, int count, string scope)
        => auditTrail.RecordAsync(eventName, new SurveyAuditEntry
        {
            ActorId = CurrentUserId(),
            Detail = string.IsNullOrEmpty(scope) ? count.ToString(CultureInfo.InvariantCulture) : $"{count.ToString(CultureInfo.InvariantCulture)} ({scope})",
        });

    /// <summary>
    /// Makes a refused export visible: the step says why it is empty, the log has a warning, and the audit trail records
    /// the refusal, so an empty step is never mistaken for a survey with nothing to export.
    /// </summary>
    /// <param name="auditTrail">The audit trail.</param>
    /// <param name="step">The recipe step written.</param>
    /// <param name="kind">What was refused, such as "SurveyResponses".</param>
    /// <returns>A task.</returns>
    public async Task RefusedAsync(ISurveyAuditTrail auditTrail, JsonObject step, string kind)
    {
        ArgumentNullException.ThrowIfNull(step);

        step[RefusedProperty] = "The user who ran the export may not export survey responses, so no record was written.";
        _logger.LogWarning("The '{Step}' export step wrote no record: the user '{UserId}' may not export survey responses.", kind, CurrentUserId());

        await auditTrail.RecordAsync(SurveyAuditEvents.ExportRefused, new SurveyAuditEntry
        {
            ActorId = CurrentUserId(),
            Detail = kind,
        });
    }
}
