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
        policy.Stages = [SeededStage()];
        policy.RequiredApprovals = 1;
        policy.Type = PayrollConstants.PayRunSubjectKind;
        policy.AllowRequesterToApprove = settings?.AllowPreparerToApprove == true;
        policy.AllowSubjectToDecide = settings?.AllowPreparerToApprove == true;
        policy.DueAfterHours = 48;
        await _policies.CreateAsync(policy);
    }

    /// <summary>
    /// Gives the seeded policy's one stage the title of its rule, "Payroll approver", while the policy still has the
    /// rule it was seeded with: a stage without a title showed the editor's example, "Manager", which its rule is not.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task TitleSeededStageAsync()
    {
        if (await _policies.FindByNameAsync(PayrollConstants.PayRunApprovalPolicy) is not { } policy)
        {
            return;
        }

        var stages = policy.GetStages();

        if (stages.Count != 1
            || !string.IsNullOrWhiteSpace(stages[0].Title)
            || stages[0].Approvers is not [{ Kind: ApproverRuleKinds.Permission } rule]
            || !string.Equals(rule.Value, PayrollPermissions.ApprovePayRuns.Name, StringComparison.Ordinal))
        {
            return;
        }

        var stage = SeededStage();
        stage.RequiredApprovals = stages[0].RequiredApprovals;
        policy.Stages = [stage];
        policy.Approvers = [];
        await _policies.UpdateAsync(policy);
    }

    /// <summary>
    /// Makes the pay run approval policy serve pay runs only, when it lists no approval type.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task ServePayRunsOnlyAsync()
    {
        if (await _policies.FindByNameAsync(PayrollConstants.PayRunApprovalPolicy) is { } policy && string.IsNullOrEmpty(policy.Type) && policy.Types is not { Count: > 0 })
        {
            policy.Type = PayrollConstants.PayRunSubjectKind;
            await _policies.UpdateAsync(policy);
        }
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

    // Anyone who may approve pay runs, for every employer or for the run's employer.
    private ApprovalStage SeededStage()
        => new()
        {
            Title = S["Payroll approver"],
            RequiredApprovals = 1,
            Approvers = [new ApproverRule { Kind = ApproverRuleKinds.Permission, Value = PayrollPermissions.ApprovePayRuns.Name }],
        };
}
