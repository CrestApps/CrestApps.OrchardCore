using System.Globalization;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using CrestApps.OrchardCore.Approvals.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Approvals.Drivers;

/// <summary>
/// The standard conditions of a policy, those its approval type opts into (<see cref="ApprovalType.Conditions"/>): an
/// amount range and currency, and a scope picked from the type's scopes, such as an employer. A general policy, saved
/// before policies had one type, shows both so nothing it holds is hidden. A type's other conditions come from its own
/// driver.
/// </summary>
internal sealed class ApprovalPolicyConditionsDisplayDriver : DisplayDriver<ApprovalPolicy>
{
    private readonly IApprovalTypeCatalog _types;

    internal readonly IStringLocalizer S;

    public ApprovalPolicyConditionsDisplayDriver(IApprovalTypeCatalog types, IStringLocalizer<ApprovalPolicyConditionsDisplayDriver> stringLocalizer)
    {
        _types = types;
        S = stringLocalizer;
    }

    public override bool CanHandleModel(ApprovalPolicy policy)
        => Conditions(policy) != ApprovalConditions.None;

    public override IDisplayResult Edit(ApprovalPolicy policy, BuildEditorContext context)
        => Initialize<ApprovalPolicyConditionsViewModel>("ApprovalPolicyConditions_Edit", async model =>
        {
            var conditions = Conditions(policy);

            model.ShowsAmount = conditions.HasFlag(ApprovalConditions.Amount);
            model.ShowsScope = conditions.HasFlag(ApprovalConditions.Scope);
            model.MinimumAmount = policy.MinimumAmount?.ToString("0.##########", CultureInfo.InvariantCulture);
            model.MaximumAmount = policy.MaximumAmount?.ToString("0.##########", CultureInfo.InvariantCulture);
            model.Currency = policy.Currency;
            model.AccessScope = policy.AccessScope;

            if (model.ShowsScope && !string.IsNullOrEmpty(policy.Type))
            {
                model.ScopeOptions = [.. (await _types.GetHandler(policy.Type).GetScopesAsync()).Select(scope => new SelectListItem(scope.DisplayText, scope.Value, scope.Value == policy.AccessScope))];

                // A scope no longer offered, such as an employer removed since, stays chosen and shown by its value.
                if (!string.IsNullOrEmpty(policy.AccessScope) && model.ScopeOptions.Count > 0 && model.ScopeOptions.All(option => option.Value != policy.AccessScope))
                {
                    model.ScopeOptions.Add(new SelectListItem(policy.AccessScope, policy.AccessScope, true));
                }
            }
        }).Location("Content:2");

    public override async Task<IDisplayResult> UpdateAsync(ApprovalPolicy policy, UpdateEditorContext context)
    {
        var model = new ApprovalPolicyConditionsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var conditions = Conditions(policy);

        // A condition the type doesn't use is cleared, so it can't keep choosing the policy unseen.
        policy.MinimumAmount = conditions.HasFlag(ApprovalConditions.Amount) ? Amount(model.MinimumAmount, nameof(model.MinimumAmount), context) : null;
        policy.MaximumAmount = conditions.HasFlag(ApprovalConditions.Amount) ? Amount(model.MaximumAmount, nameof(model.MaximumAmount), context) : null;
        policy.Currency = conditions.HasFlag(ApprovalConditions.Amount) && !string.IsNullOrWhiteSpace(model.Currency) ? model.Currency.Trim().ToUpperInvariant() : null;
        policy.AccessScope = conditions.HasFlag(ApprovalConditions.Scope) && !string.IsNullOrWhiteSpace(model.AccessScope) ? model.AccessScope.Trim() : null;

        return Edit(policy, context);
    }

    // The conditions of the policy's type; a general policy shows them all.
    private ApprovalConditions Conditions(ApprovalPolicy policy)
        => string.IsNullOrEmpty(policy?.Type)
            ? ApprovalConditions.Amount | ApprovalConditions.Scope
            : _types.Find(policy.Type)?.Conditions ?? ApprovalConditions.None;

    // Amounts are typed as text with a dot so a decimal comma never changes them; the handler checks their order.
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
}
