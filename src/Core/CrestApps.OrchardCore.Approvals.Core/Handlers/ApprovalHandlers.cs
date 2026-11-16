using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Approvals.Core.Handlers;

/// <summary>
/// Stamps approval requests.
/// </summary>
public sealed class ApprovalRequestHandler : CatalogEntryHandlerBase<ApprovalRequest>
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalRequestHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    public ApprovalRequestHandler(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc />
    public override Task CreatingAsync(CreatingContext<ApprovalRequest> context, CancellationToken cancellationToken = default)
    {
        if (context.Model.CreatedUtc == default)
        {
            context.Model.CreatedUtc = _clock.UtcNow;
        }

        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<ApprovalRequest> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;

        return Task.CompletedTask;
    }
}

/// <summary>
/// Fills approval policies from recipes, stamps and checks them.
/// </summary>
public sealed class ApprovalPolicyHandler : CatalogEntryHandlerBase<ApprovalPolicy>
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IClock _clock;
    private readonly IEnumerable<IApproverRuleProvider> _providers;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalPolicyHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="providers">The approver rule providers, to check the rules that need a value.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalPolicyHandler(IClock clock, IEnumerable<IApproverRuleProvider> providers, IStringLocalizer<ApprovalPolicyHandler> stringLocalizer)
    {
        _clock = clock;
        _providers = providers;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<ApprovalPolicy> context, CancellationToken cancellationToken = default)
    {
        Populate(context.Model, context.Data);
        OneType(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task LoadedAsync(LoadedContext<ApprovalPolicy> context, CancellationToken cancellationToken = default)
    {
        OneType(context.Model);

        return Task.CompletedTask;
    }

    // A policy saved when policies served a list of types, with one in it, is that type's policy; with several or none it
    // stays a general policy.
    private static void OneType(ApprovalPolicy policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Type) && policy.Types is { Count: 1 })
        {
            policy.Type = policy.Types[0];
            policy.Types = [];
        }
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<ApprovalPolicy> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;
        Populate(context.Model, context.Data);
        OneType(context.Model);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidatingAsync(ValidatingContext<ApprovalPolicy> context, CancellationToken cancellationToken = default)
    {
        var policy = context.Model;

        if (string.IsNullOrWhiteSpace(policy.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ApprovalPolicy.Name)]));
        }

        var stages = policy.GetStages();

        if (stages.Count > 10)
        {
            context.Result.Fail(new ValidationResult(S["A policy has ten stages at most."], [nameof(ApprovalPolicy.Stages)]));
        }

        foreach (var (stage, index) in stages.Select((stage, index) => (stage, index + 1)))
        {
            if (stage.RequiredApprovals is < 1 or > 10)
            {
                context.Result.Fail(new ValidationResult(S["One to ten approvals can be required at stage {0}.", index], [nameof(ApprovalPolicy.RequiredApprovals)]));
            }

            if (stage.Approvers.Count == 0)
            {
                context.Result.Fail(new ValidationResult(S["Say who may approve at stage {0}: a permission, a role, named users, the manager or another approver rule.", index], [nameof(ApprovalPolicy.Approvers)]));
            }

            foreach (var rule in stage.Approvers)
            {
                var provider = _providers.FirstOrDefault(candidate => string.Equals(candidate.Name, rule.Kind, StringComparison.OrdinalIgnoreCase));

                // A rule of a provider whose feature is off is kept: it names nobody until the feature is on again.
                if (string.IsNullOrWhiteSpace(rule.Kind))
                {
                    context.Result.Fail(new ValidationResult(S["Each approver rule at stage {0} needs a kind.", index], [nameof(ApprovalPolicy.Approvers)]));
                }
                else if (provider?.NeedsValue == true && string.IsNullOrWhiteSpace(rule.Value))
                {
                    context.Result.Fail(new ValidationResult(S["The approver rule {0} at stage {1} needs a value.", rule.Kind, index], [nameof(ApprovalPolicy.Approvers)]));
                }
            }
        }

        if (policy.MinimumAmount is { } minimum && policy.MaximumAmount is { } maximum && minimum > maximum)
        {
            context.Result.Fail(new ValidationResult(S["The least amount is more than the most amount."], [nameof(ApprovalPolicy.MinimumAmount)]));
        }

        if (policy.DueAfterHours is < 1)
        {
            context.Result.Fail(new ValidationResult(S["A request is due one hour or more after it is made."], [nameof(ApprovalPolicy.DueAfterHours)]));
        }

        return Task.CompletedTask;
    }

    private static void Populate(ApprovalPolicy policy, JsonNode data)
    {
        if (data is not JsonObject json || json.Deserialize<ApprovalPolicy>(_jsonOptions) is not { } values)
        {
            return;
        }

        policy.Name = values.Name?.Trim() ?? policy.Name;
        policy.Title = json.ContainsKey(nameof(ApprovalPolicy.Title)) ? values.Title : policy.Title;
        policy.Description = json.ContainsKey(nameof(ApprovalPolicy.Description)) ? values.Description : policy.Description;
        policy.Approvers = json.ContainsKey(nameof(ApprovalPolicy.Approvers)) ? values.Approvers ?? [] : policy.Approvers;
        policy.RequiredApprovals = json.ContainsKey(nameof(ApprovalPolicy.RequiredApprovals)) ? values.RequiredApprovals : policy.RequiredApprovals;
        policy.AllowRequesterToApprove = json.ContainsKey(nameof(ApprovalPolicy.AllowRequesterToApprove)) ? values.AllowRequesterToApprove : policy.AllowRequesterToApprove;
        policy.AllowSubjectToDecide = json.ContainsKey(nameof(ApprovalPolicy.AllowSubjectToDecide)) ? values.AllowSubjectToDecide : policy.AllowSubjectToDecide;
        policy.AllowSamePersonAcrossStages = json.ContainsKey(nameof(ApprovalPolicy.AllowSamePersonAcrossStages)) ? values.AllowSamePersonAcrossStages : policy.AllowSamePersonAcrossStages;
        policy.DueAfterHours = json.ContainsKey(nameof(ApprovalPolicy.DueAfterHours)) ? values.DueAfterHours : policy.DueAfterHours;
        policy.Type = json.ContainsKey(nameof(ApprovalPolicy.Type)) ? values.Type?.Trim() : policy.Type;
        policy.Types = json.ContainsKey(nameof(ApprovalPolicy.Types)) ? values.Types ?? [] : policy.Types;
        policy.Enabled = json.ContainsKey(nameof(ApprovalPolicy.Enabled)) ? values.Enabled : policy.Enabled;
        policy.Order = json.ContainsKey(nameof(ApprovalPolicy.Order)) ? values.Order : policy.Order;
        policy.MinimumAmount = json.ContainsKey(nameof(ApprovalPolicy.MinimumAmount)) ? values.MinimumAmount : policy.MinimumAmount;
        policy.MaximumAmount = json.ContainsKey(nameof(ApprovalPolicy.MaximumAmount)) ? values.MaximumAmount : policy.MaximumAmount;
        policy.Currency = json.ContainsKey(nameof(ApprovalPolicy.Currency)) ? values.Currency : policy.Currency;
        policy.AccessScope = json.ContainsKey(nameof(ApprovalPolicy.AccessScope)) ? values.AccessScope : policy.AccessScope;
        policy.Stages = json.ContainsKey(nameof(ApprovalPolicy.Stages)) ? values.Stages ?? [] : policy.Stages;
    }
}
