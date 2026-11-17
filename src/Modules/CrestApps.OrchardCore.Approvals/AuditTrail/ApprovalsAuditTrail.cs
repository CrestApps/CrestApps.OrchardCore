using System.Security.Claims;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Approvals.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OrchardCore.AuditTrail.Services;
using OrchardCore.AuditTrail.Services.Models;

namespace CrestApps.OrchardCore.Approvals.AuditTrail;

/// <summary>
/// The Audit Trail events of Approvals.
/// </summary>
internal sealed class ApprovalsAuditTrailEvents : IConfigureOptions<AuditTrailOptions>
{
    public const string CategoryName = "Approvals";
    public const string PolicyCreated = "ApprovalPolicyCreated";
    public const string PolicyUpdated = "ApprovalPolicyUpdated";
    public const string PolicyDeleted = "ApprovalPolicyDeleted";

    public void Configure(AuditTrailOptions options)
    {
        options.For<ApprovalsAuditTrailEvents>(CategoryName, S => S["Approvals"])
            .WithEvent(PolicyCreated, S => S["Approval policy added"], S => S["Someone added an approval policy."], enableByDefault: true)
            .WithEvent(PolicyUpdated, S => S["Approval policy changed"], S => S["Someone changed who may decide under an approval policy, or how many must."], enableByDefault: true)
            .WithEvent(PolicyDeleted, S => S["Approval policy removed"], S => S["Someone removed an approval policy."], enableByDefault: true);
    }
}

/// <summary>
/// What the Audit Trail keeps of an approval policy.
/// </summary>
public sealed class ApprovalPolicyAuditData
{
    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how many approvals are required.
    /// </summary>
    public int RequiredApprovals { get; set; }

    /// <summary>
    /// Gets or sets the approver rules, one per line.
    /// </summary>
    public IList<string> Approvers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the requester may approve.
    /// </summary>
    public bool AllowRequesterToApprove { get; set; }

    /// <summary>
    /// Gets or sets whether a person may decide a request about themselves.
    /// </summary>
    public bool AllowSubjectToDecide { get; set; }
}

/// <summary>
/// Writes each change of an approval policy to the Audit Trail, with who made it.
/// </summary>
internal sealed class ApprovalPolicyAuditTrailHandler : CatalogEntryHandlerBase<ApprovalPolicy>
{
    private readonly IAuditTrailManager _auditTrailManager;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApprovalPolicyAuditTrailHandler(IAuditTrailManager auditTrailManager, IHttpContextAccessor httpContextAccessor)
    {
        _auditTrailManager = auditTrailManager;
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task CreatedAsync(CreatedContext<ApprovalPolicy> context, CancellationToken cancellationToken = default)
        => RecordAsync(ApprovalsAuditTrailEvents.PolicyCreated, context.Model);

    public override Task UpdatedAsync(UpdatedContext<ApprovalPolicy> context, CancellationToken cancellationToken = default)
        => RecordAsync(ApprovalsAuditTrailEvents.PolicyUpdated, context.Model);

    public override Task DeletedAsync(DeletedContext<ApprovalPolicy> context, CancellationToken cancellationToken = default)
        => RecordAsync(ApprovalsAuditTrailEvents.PolicyDeleted, context.Model);

    private Task RecordAsync(string name, ApprovalPolicy policy)
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return _auditTrailManager.RecordEventAsync(new AuditTrailContext<ApprovalPolicyAuditData>(
            name,
            ApprovalsAuditTrailEvents.CategoryName,
            policy.ItemId,
            userId: user?.FindFirstValue(ClaimTypes.NameIdentifier),
            userName: user?.Identity?.Name,
            new ApprovalPolicyAuditData
            {
                Name = policy.Name,
                Title = policy.Title,
                RequiredApprovals = policy.RequiredApprovals,
                Approvers = policy.Approvers.Select(rule => string.IsNullOrEmpty(rule.Value) ? rule.Kind.ToString() : $"{rule.Kind}: {rule.Value}").ToList(),
                AllowRequesterToApprove = policy.AllowRequesterToApprove,
                AllowSubjectToDecide = policy.AllowSubjectToDecide,
            }));
    }
}
