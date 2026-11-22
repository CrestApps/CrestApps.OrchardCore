using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.AspNetCore.Identity;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.AuditTrail;

/// <summary>
/// Records Surveys events in the audit trail.
/// </summary>
internal sealed class AuditTrailSurveyAuditTrail : ISurveyAuditTrail
{
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly UserManager<IUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditTrailSurveyAuditTrail"/> class.
    /// </summary>
    /// <param name="auditTrailManager">The audit trail manager.</param>
    /// <param name="userManager">The user manager, to record the actor's user name next to the id.</param>
    public AuditTrailSurveyAuditTrail(
        IAuditTrailManager auditTrailManager,
        UserManager<IUser> userManager)
    {
        _auditTrailManager = auditTrailManager;
        _userManager = userManager;
    }

    /// <inheritdoc />
    public async Task RecordAsync(string eventName, SurveyAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(entry);

        var data = new SurveyAuditData
        {
            SurveyId = entry.SurveyId,
            SurveyName = entry.SurveyName,
            ResponseId = entry.ResponseId,
            ResponseNumber = entry.ResponseNumber,
            VersionNumber = entry.VersionNumber,
            From = entry.From,
            To = entry.To,
            Reason = entry.Reason,
            Detail = entry.Detail,
        };

        var correlationId = entry.ResponseId ?? entry.SurveyId ?? eventName;

        var userName = string.IsNullOrEmpty(entry.ActorId)
            ? null
            : (await _userManager.FindByIdAsync(entry.ActorId))?.UserName;

        await _auditTrailManager.RecordEventAsync(new AuditTrailContext<SurveyAuditData>(eventName, SurveyAuditEvents.Category, correlationId, entry.ActorId, userName, data));
    }
}
