using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Staff.Services;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// The managers up the chain of the staff member the request is about (<see cref="ApprovalRequest.SubjectStaffMemberId"/>),
/// who have a user. Registered with Staff on.
/// </summary>
internal sealed class ManagerApproverRuleProvider : IApproverRuleProvider
{
    private readonly IStaffHierarchy _hierarchy;

    public ManagerApproverRuleProvider(IStaffHierarchy hierarchy)
    {
        _hierarchy = hierarchy;
    }

    public string Name
        => ApproverRuleKinds.Manager;

    [JsonIgnore]
    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<ManagerApproverRuleProvider>("The staff member's manager");

    public bool NeedsValue
        => false;

    public bool DependsOnRequest
        => true;

    public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id && (await GetUserIdsAsync(rule, request, cancellationToken)).Contains(id, StringComparer.Ordinal);

    public async Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request?.SubjectStaffMemberId))
        {
            return [];
        }

        return [.. (await _hierarchy.GetManagerChainAsync(request.SubjectStaffMemberId, cancellationToken))
            .Select(manager => manager.UserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)];
    }
}

/// <summary>
/// Named staff members (the rule's value is their ids, separated by commas), by their users. Registered with Staff on.
/// </summary>
internal sealed class StaffApproverRuleProvider : IApproverRuleProvider
{
    private readonly IStaffDirectory _directory;

    public StaffApproverRuleProvider(IStaffDirectory directory)
    {
        _directory = directory;
    }

    public string Name
        => ApproverRuleKinds.Staff;

    [JsonIgnore]
    public LocalizationSource DisplayName { get; } = LocalizationSource.Create<StaffApproverRuleProvider>("Named staff members");

    public bool NeedsValue
        => true;

    public bool DependsOnRequest
        => false;

    public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id && (await GetUserIdsAsync(rule, request, cancellationToken)).Contains(id, StringComparer.Ordinal);

    public async Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var ids = (rule?.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (ids.Length == 0)
        {
            return [];
        }

        return [.. (await _directory.GetAsync(ids, cancellationToken)).Values
            .Select(member => member.UserId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)];
    }
}
