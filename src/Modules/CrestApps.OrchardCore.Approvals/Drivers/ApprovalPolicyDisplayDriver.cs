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

    public override IDisplayResult Edit(ApprovalPolicy policy, BuildEditorContext context)
    {
        return Initialize<ApprovalPolicyViewModel>("ApprovalPolicyFields_Edit", model =>
        {
            model.Name = policy.Name;
            model.Title = policy.Title;
            model.Description = policy.Description;
            model.Types = [.. policy.Types ?? []];
            model.Enabled = policy.Enabled;
            model.Order = policy.Order == 0 ? null : policy.Order.ToString(CultureInfo.InvariantCulture);
            model.MinimumAmount = policy.MinimumAmount?.ToString("0.##########", CultureInfo.InvariantCulture);
            model.MaximumAmount = policy.MaximumAmount?.ToString("0.##########", CultureInfo.InvariantCulture);
            model.Currency = policy.Currency;
            model.AccessScope = policy.AccessScope;
            model.AllowRequesterToApprove = policy.AllowRequesterToApprove;
            model.AllowSubjectToDecide = policy.AllowSubjectToDecide;
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

            model.TypeOptions = [.. _types.List().Select(type => new SelectListItem(Localize(type.DisplayName) ?? type.Name, type.Name, model.Types.Contains(type.Name, StringComparer.OrdinalIgnoreCase)))];

            // A type of a feature that is off is kept on the policy and shown by its name.
            foreach (var missing in model.Types.Where(type => model.TypeOptions.All(option => !string.Equals(option.Value, type, StringComparison.OrdinalIgnoreCase))))
            {
                model.TypeOptions.Add(new SelectListItem(missing, missing, true));
            }

            model.KindOptions = [new SelectListItem(S["Not used"], string.Empty), .. _providers
                .OrderBy(provider => provider.Name, StringComparer.Ordinal)
                .Select(provider => new SelectListItem(Localize(provider.DisplayName) ?? provider.Name, provider.Name))];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ApprovalPolicy policy, UpdateEditorContext context)
    {
        var model = new ApprovalPolicyViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        policy.Name = Clean(model.Name);
        policy.Title = Clean(model.Title);
        policy.Description = Clean(model.Description);
        policy.Types = [.. (model.Types ?? []).Where(type => !string.IsNullOrWhiteSpace(type)).Select(type => type.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)];
        policy.Enabled = model.Enabled;
        policy.Order = Whole(model.Order, nameof(model.Order), context) ?? 0;
        policy.MinimumAmount = Amount(model.MinimumAmount, nameof(model.MinimumAmount), context);
        policy.MaximumAmount = Amount(model.MaximumAmount, nameof(model.MaximumAmount), context);
        policy.Currency = Clean(model.Currency)?.ToUpperInvariant();
        policy.AccessScope = Clean(model.AccessScope);
        policy.AllowRequesterToApprove = model.AllowRequesterToApprove;
        policy.AllowSubjectToDecide = model.AllowSubjectToDecide;

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

    private decimal? Amount(string text, string field, UpdateEditorContext context)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        context.Updater.ModelState.AddModelError(Prefix, field, S["Enter an amount with a dot for decimals, or leave it empty."]);

        return null;
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
