namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The kinds of the messages a buyer gets about an installment plan, which the store can write its own templates of.
/// </summary>
public static class InstallmentPlanEmailKinds
{
    /// <summary>
    /// The name the store's template reads the plan under, as <c>{{ InstallmentPlan }}</c>.
    /// </summary>
    public const string PlanModelName = "InstallmentPlan";

    /// <summary>
    /// A notice about the plan, such as a payment that failed or a plan that was canceled.
    /// </summary>
    public const string Notice = "installment-plan-notice";
}
