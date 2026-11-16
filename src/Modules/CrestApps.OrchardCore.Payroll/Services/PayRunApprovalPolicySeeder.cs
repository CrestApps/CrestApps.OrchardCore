using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Payroll.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Payroll.Services;

/// <summary>
/// Adds Approvals' pay run policy when there is none, and keeps its separation of duties in step with Payroll's
/// settings: by default the person who prepared or submitted a run may not approve it.
/// </summary>
public sealed class PayRunApprovalPolicySeeder
{
    private readonly INamedCatalogManager<ApprovalPolicy> _policies;
    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public PayRunApprovalPolicySeeder(INamedCatalogManager<ApprovalPolicy> policies, ISiteService siteService, IStringLocalizer<PayRunApprovalPolicySeeder> stringLocalizer)
    {
        _policies = policies;
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <summary>
    /// Adds the policy when it is missing.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task SeedAsync()
    {
        if (await _policies.FindByNameAsync(PayrollConstants.PayRunApprovalPolicy) is not null)
        {
            return;
        }

        var settings = await _siteService.GetSettingsAsync<PayrollSettings>();
        var policy = await _policies.NewAsync();

        policy.Name = PayrollConstants.PayRunApprovalPolicy;
        policy.Title = S["Pay run approval"];
        policy.Description = S["A pay run is approved before it is processed. People who approve pay runs, for every employer or for the run's employer, decide."];
        policy.Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Permission, Value = PayrollPermissions.ApprovePayRuns.Name }];
        policy.RequiredApprovals = 1;
        policy.Types = [PayrollConstants.PayRunSubjectKind];
        policy.AllowRequesterToApprove = settings?.AllowPreparerToApprove == true;
        policy.AllowSubjectToDecide = settings?.AllowPreparerToApprove == true;
        policy.DueAfterHours = 48;
        await _policies.CreateAsync(policy);
    }

    /// <summary>
    /// Copies the settings' separation of duties onto the policy.
    /// </summary>
    /// <param name="allowPreparerToApprove">Whether the person who prepared a run may approve it.</param>
    /// <returns>A task.</returns>
    public async Task SyncAsync(bool allowPreparerToApprove)
    {
        if (await _policies.FindByNameAsync(PayrollConstants.PayRunApprovalPolicy) is { } policy
            && (policy.AllowRequesterToApprove != allowPreparerToApprove || policy.AllowSubjectToDecide != allowPreparerToApprove))
        {
            policy.AllowRequesterToApprove = allowPreparerToApprove;
            policy.AllowSubjectToDecide = allowPreparerToApprove;
            await _policies.UpdateAsync(policy);
        }
    }
}
