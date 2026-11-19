using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The default <see cref="IApproverDirectory"/>: asks the approver rule provider a rule names. A rule whose provider
/// isn't registered (its feature is off) names nobody.
/// </summary>
public sealed class ApproverDirectory : IApproverDirectory
{
    private readonly IEnumerable<IApproverRuleProvider> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApproverDirectory"/> class.
    /// </summary>
    /// <param name="providers">The approver rule providers.</param>
    public ApproverDirectory(IEnumerable<IApproverRuleProvider> providers)
    {
        _providers = providers;
    }

    /// <inheritdoc />
    public async Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(request);

        if (user?.Identity?.IsAuthenticated != true || Find(rule) is not { } provider)
        {
            return false;
        }

        return await provider.IsNamedAsync(rule, request, user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(request);

        return Find(rule) is { } provider ? await provider.GetUserIdsAsync(rule, request, cancellationToken) : [];
    }

    /// <inheritdoc />
    public bool DependsOnRequest(ApproverRule rule)
        => rule is not null && Find(rule)?.DependsOnRequest == true;

    private IApproverRuleProvider Find(ApproverRule rule)
        => string.IsNullOrWhiteSpace(rule.Kind)
            ? null
            : _providers.FirstOrDefault(provider => string.Equals(provider.Name, rule.Kind.Trim(), StringComparison.OrdinalIgnoreCase));
}
