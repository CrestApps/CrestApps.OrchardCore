using System.Globalization;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Subscriptions.AI;

/// <summary>
/// An installment plan tool: it needs <see cref="SubscriptionPermissions.ManageInstallmentPlans"/>, the permission of
/// Subscriptions › Installment Plans, and shares finding a plan and describing it from an explicit list of fields. The
/// customer's email, the saved card's provider references and the checkout sessions are never returned; a saved card is
/// described by its brand and last four digits only, as the page shows it.
/// </summary>
internal abstract class InstallmentPlanToolBase : SubscriptionsAIToolBase
{
    /// <inheritdoc/>
    protected override Permission Permission => SubscriptionPermissions.ManageInstallmentPlans;

    /// <summary>
    /// Finds a plan by its id.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="id">The plan's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The plan, or the sentence that says it was not found.</returns>
    protected static async Task<(InstallmentPlan Plan, string Error)> FindPlanAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id))
        {
            return (null, "Nothing was done: give the plan's planId. Use listInstallmentPlans to find it.");
        }

        var plan = await services.GetRequiredService<IInstallmentPlanStore>().FindByIdAsync(id, cancellationToken);

        return plan is null
            ? (null, $"Nothing was done: no installment plan has the id '{id}'. Use listInstallmentPlans to find it.")
            : (plan, null);
    }

    /// <summary>
    /// Describes a plan for a list row or the head of its details.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The allow-listed fields.</returns>
    protected static async Task<Dictionary<string, object>> SummarizeAsync(IServiceProvider services, InstallmentPlan plan)
    {
        var next = plan.NextPayment;

        return new Dictionary<string, object>
        {
            ["id"] = plan.ItemId,
            ["title"] = plan.Title,
            ["customer"] = plan.CustomerName,
            ["customerUserId"] = plan.OwnerId,
            ["status"] = ToCamel(plan.Status.ToString()),
            ["currency"] = plan.Currency,
            ["total"] = plan.TotalAmount,
            ["downPayment"] = plan.DownPaymentAmount,
            ["paid"] = plan.AmountPaid,
            ["outstanding"] = plan.AmountOutstanding,
            ["payments"] = plan.InstallmentCount,
            ["frequency"] = ToCamel(plan.Frequency.ToString()),
            ["collectionMethod"] = ToCamel(plan.CollectionMethod.ToString()),
            ["nextPayment"] = next is null
                ? null
                : new
                {
                    number = next.Number,
                    due = DueDate(next.DueUtc),
                    amount = next.Amount,
                    status = ToCamel(next.Status.ToString()),
                },
            ["created"] = await LocalTimeAsync(services, plan.CreatedUtc),
        };
    }

    /// <summary>
    /// Writes a payment's due date. Due dates are pure dates, entered as dates and kept at midnight UTC, so they are
    /// shown as entered rather than converted to a time zone.
    /// </summary>
    /// <param name="dueUtc">The stored due date.</param>
    /// <returns>The date, such as 2026-12-01.</returns>
    protected static string DueDate(DateTime dueUtc)
        => dueUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Gets the admin path of a plan's page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The path, or <see langword="null"/>.</returns>
    protected static string GetPlanPath(IServiceProvider services, InstallmentPlan plan)
        => GetAdminPath(services, "InstallmentPlansDetail", new { area = SubscriptionConstants.Features.Area, itemId = plan.ItemId });

    /// <summary>
    /// Gets the admin path where staff take a draft plan's first payment by card, when it is taken there.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="plan">The plan.</param>
    /// <returns>The path, or <see langword="null"/> when the plan does not wait for a payment taken on that page.</returns>
    protected static string GetPayPath(IServiceProvider services, InstallmentPlan plan)
        => plan.Status == InstallmentPlanStatus.Draft && string.IsNullOrEmpty(plan.SourceTransactionId)
            ? GetAdminPath(services, "InstallmentPlansPay", new { area = SubscriptionConstants.Features.Area, itemId = plan.ItemId })
            : null;
}
