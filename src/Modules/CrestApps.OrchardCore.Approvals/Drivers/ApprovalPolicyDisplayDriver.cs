using System.Globalization;
using CrestApps.OrchardCore.Approvals.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
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
/// The rows of the policy list and the policy editor.
/// </summary>
internal sealed class ApprovalPolicyDisplayDriver : DisplayDriver<ApprovalPolicy>
{
    private readonly IApproverDirectory _directory;
    private readonly INotifier _notifier;

    internal readonly IStringLocalizer S;
    internal readonly IHtmlLocalizer H;

    public ApprovalPolicyDisplayDriver(
        IApproverDirectory directory,
        INotifier notifier,
        IStringLocalizer<ApprovalPolicyDisplayDriver> stringLocalizer,
        IHtmlLocalizer<ApprovalPolicyDisplayDriver> htmlLocalizer)
    {
        _directory = directory;
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
            model.RequiredApprovals = policy.RequiredApprovals;
            model.AllowRequesterToApprove = policy.AllowRequesterToApprove;
            model.AllowSubjectToDecide = policy.AllowSubjectToDecide;
            model.DueAfterHours = policy.DueAfterHours?.ToString(CultureInfo.InvariantCulture);
            model.IsNew = context.IsNew;
            model.Approvers = policy.Approvers.Select(rule => new ApproverRuleViewModel { Kind = rule.Kind.ToString(), Value = rule.Value }).ToList();

            while (model.Approvers.Count < ApprovalPolicyViewModel.RuleSlots)
            {
                model.Approvers.Add(new ApproverRuleViewModel());
            }

            model.KindOptions =
            [
                new SelectListItem(S["Not used"], string.Empty),
                new SelectListItem(S["Anyone with the permission"], nameof(ApproverKind.Permission)),
                new SelectListItem(S["Anyone in the role"], nameof(ApproverKind.Role)),
                new SelectListItem(S["The staff member's manager"], nameof(ApproverKind.Manager)),
                new SelectListItem(S["Named staff members"], nameof(ApproverKind.Staff)),
            ];
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(ApprovalPolicy policy, UpdateEditorContext context)
    {
        var model = new ApprovalPolicyViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        policy.Name = Clean(model.Name);
        policy.Title = Clean(model.Title);
        policy.Description = Clean(model.Description);
        policy.RequiredApprovals = model.RequiredApprovals;
        policy.AllowRequesterToApprove = model.AllowRequesterToApprove;
        policy.AllowSubjectToDecide = model.AllowSubjectToDecide;
        policy.Approvers = (model.Approvers ?? [])
            .Where(rule => Enum.TryParse<ApproverKind>(rule.Kind, out _))
            .Select(rule => new ApproverRule { Kind = Enum.Parse<ApproverKind>(rule.Kind), Value = Clean(rule.Value) })
            .ToList();

        if (string.IsNullOrWhiteSpace(model.DueAfterHours))
        {
            policy.DueAfterHours = null;
        }
        else if (int.TryParse(model.DueAfterHours.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var hours) && hours > 0)
        {
            policy.DueAfterHours = hours;
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DueAfterHours), S["Enter a number of hours, or leave it empty."]);
        }

        // A policy no one can satisfy is saved, with a warning: the people may come later.
        if (context.Updater.ModelState.IsValid && policy.Approvers.Count > 0 && await _directory.CountPeopleAsync(policy) is { } people && people < policy.RequiredApprovals)
        {
            await _notifier.WarningAsync(H["The policy needs {0} approvals, but only {1} can decide under its rules today. Requests under it can't be approved until more people can.", policy.RequiredApprovals, people]);
        }

        return Edit(policy, context);
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
