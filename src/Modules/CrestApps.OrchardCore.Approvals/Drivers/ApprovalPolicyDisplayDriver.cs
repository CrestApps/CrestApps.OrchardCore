using System.Globalization;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Approvals.ViewModels;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Approvals.Drivers;

/// <summary>
/// The rows of the policy list and the policy editor: the approval types it serves, the conditions it is chosen by,
/// its stages with their approver rules (each rule's value from its provider's editor shape), and the built-in rules.
/// </summary>
internal sealed class ApprovalPolicyDisplayDriver : DisplayDriver<ApprovalPolicy>
{
    private readonly IApproverDirectory _directory;
    private readonly IApprovalTypeCatalog _types;
    private readonly IEnumerable<IApproverRuleProvider> _providers;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly INotifier _notifier;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public ApprovalPolicyDisplayDriver(
        IApproverDirectory directory,
        IApprovalTypeCatalog types,
        IEnumerable<IApproverRuleProvider> providers,
        IStringLocalizerFactory localizerFactory,
        INotifier notifier,
        IStringLocalizer<ApprovalPolicyDisplayDriver> stringLocalizer,
        IHtmlLocalizer<ApprovalPolicyDisplayDriver> htmlLocalizer)
    {
        _directory = directory;
        _types = types;
        _providers = providers;
        _localizerFactory = localizerFactory;
        _notifier = notifier;
        S = stringLocalizer;
        H = htmlLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ApprovalPolicy policy, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{policy.Name} {policy.Title}".ToLowerInvariant();
        }

        return CombineAsync(
            View("ApprovalPolicy_Fields_SummaryAdmin", policy)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ApprovalPolicy_Tags_SummaryAdmin", policy)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("ApprovalPolicy_Buttons_SummaryAdmin", policy).Location("Actions:5"),
            View("ApprovalPolicy_ActionsMenu_SummaryAdmin", policy).Location("ActionsMenu:10"));
    }

    // The policy's own fields at the top, its stages and built-in rules at the bottom; its type's conditions and fields
    // go between (Content:2 to Content:9).
    public override IDisplayResult Edit(ApprovalPolicy policy, BuildEditorContext context)
        => Combine(
            Initialize<ApprovalPolicyViewModel>("ApprovalPolicyFields_Edit", model => Fill(model, policy, context)).Location("Content:1"),
            Initialize<ApprovalPolicyViewModel>("ApprovalPolicyStages_Edit", model => Fill(model, policy, context)).Location("Content:10"));

    private void Fill(ApprovalPolicyViewModel model, ApprovalPolicy policy, BuildEditorContext context)
    {
        {
            model.Name = policy.Name;
            model.Title = policy.Title;
            model.Description = policy.Description;
            model.TypeDisplayName = TypeName(policy);
            model.Enabled = policy.Enabled;
            model.Order = policy.Order == 0 ? null : policy.Order.ToString(CultureInfo.InvariantCulture);
            model.AllowRequesterToApprove = policy.AllowRequesterToApprove;
            model.AllowSubjectToDecide = policy.AllowSubjectToDecide;
            model.AllowSamePersonAcrossStages = policy.AllowSamePersonAcrossStages;
            model.DueAfterHours = policy.DueAfterHours?.ToString(CultureInfo.InvariantCulture);
            model.IsNew = context.IsNew;

            // A policy saved before stages is shown as its one stage.
            model.Stages = [.. policy.GetStages().Select(stage => new ApprovalStageViewModel
            {
                Title = stage.Title,
                RequiredApprovals = stage.RequiredApprovals,
                Approvers = [.. stage.Approvers.Select(rule => new ApproverRuleViewModel
                {
                    Kind = rule.Kind,
                    Values = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { [rule.Kind ?? string.Empty] = [rule.Value] },
                })],
            })];

            while (model.Stages.Count < ApprovalPolicyViewModel.StageSlots)
            {
                model.Stages.Add(new ApprovalStageViewModel());
            }

            foreach (var stage in model.Stages)
            {
                while (stage.Approvers.Count < ApprovalPolicyViewModel.RuleSlots)
                {
                    stage.Approvers.Add(new ApproverRuleViewModel());
                }
            }

            model.KindOptions = [new SelectListItem(S["Not used"], string.Empty), .. _providers
                .OrderBy(provider => provider.Name, StringComparer.Ordinal)
                .Select(provider => new SelectListItem(Localize(provider.DisplayName) ?? provider.Name, provider.Name))];
        }
    }

    public override async Task<IDisplayResult> UpdateAsync(ApprovalPolicy policy, UpdateEditorContext context)
    {
        var model = new ApprovalPolicyViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        policy.Name = Clean(model.Name);
        policy.Title = Clean(model.Title);
        policy.Description = Clean(model.Description);
        policy.Enabled = model.Enabled;
        policy.Order = Whole(model.Order, nameof(model.Order), context) ?? 0;
        policy.AllowRequesterToApprove = model.AllowRequesterToApprove;
        policy.AllowSubjectToDecide = model.AllowSubjectToDecide;
        policy.AllowSamePersonAcrossStages = model.AllowSamePersonAcrossStages;

        // Each rule keeps the value of the provider chosen for it; a stage without rules isn't kept. The stages are
        // the policy's approvers from now on, so the fields of a policy saved before stages are cleared.
        policy.Stages = [.. (model.Stages ?? [])
            .Select(stage => new ApprovalStage
            {
                Title = Clean(stage.Title),
                RequiredApprovals = stage.RequiredApprovals,
                Approvers = [.. (stage.Approvers ?? [])
                    .Where(rule => !string.IsNullOrWhiteSpace(rule.Kind))
                    .Select(rule => new ApproverRule
                    {
                        Kind = rule.Kind.Trim(),
                        Value = Clean(string.Join(",", (rule.Values?.GetValueOrDefault(rule.Kind.Trim()) ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()))),
                    })],
            })
            .Where(stage => stage.Approvers.Count > 0)];
        policy.Approvers = [];
        policy.RequiredApprovals = policy.Stages.FirstOrDefault()?.RequiredApprovals ?? 1;

        // Fewer than one hour is the handler's to refuse, as for a recipe.
        policy.DueAfterHours = Whole(model.DueAfterHours, nameof(model.DueAfterHours), context);

        // A stage no one can satisfy is saved, with a warning: the people may come later.
        if (context.Updater.ModelState.IsValid)
        {
            foreach (var (stage, index) in policy.Stages.Select((stage, index) => (stage, index + 1)))
            {
                if (await _directory.CountPeopleAsync(stage) is { } people && people < stage.RequiredApprovals)
                {
                    await _notifier.WarningAsync(H["Stage {0} needs {1} approvals, but only {2} can decide under its rules today. Requests can't pass it until more people can.", index, stage.RequiredApprovals, people]);
                }
            }
        }

        return Edit(policy, context);
    }

    private string Localize(global::OrchardCore.Localization.LocalizationSource source)
        => source is null ? null : _localizerFactory.Localize(source).Value;

    // The policy's type as people see it; a general policy serves the types it lists, or every type.
    private string TypeName(ApprovalPolicy policy)
    {
        if (!string.IsNullOrEmpty(policy.Type))
        {
            return _types.Find(policy.Type) is { } type ? Localize(type.DisplayName) ?? type.Name : policy.Type;
        }

        return policy.Types is { Count: > 0 } ? S["General: {0}", string.Join(", ", policy.Types)] : S["General: any approval type"];
    }

    // Numbers are typed as text so a culture never changes them; the handler checks ranges, as for a recipe.
    private int? Whole(string text, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        context.Updater.ModelState.AddModelError(Prefix, field, S["Enter a whole number, or leave it empty."]);

        return null;
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
