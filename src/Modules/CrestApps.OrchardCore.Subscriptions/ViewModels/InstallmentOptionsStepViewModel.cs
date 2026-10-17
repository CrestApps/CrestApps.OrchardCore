namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The "Choose how to pay" checkout step: pay the balance in full, or in one of the installment options offered.
/// </summary>
public class InstallmentOptionsStepViewModel
{
    /// <summary>
    /// Gets or sets the option the customer chose, posted back by the form.
    /// </summary>
    public string SelectedOptionId { get; set; }

    /// <summary>
    /// Gets or sets what the balance is for.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the currency of every amount on the step.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the balance, paid in full.
    /// </summary>
    public decimal FullAmount { get; set; }

    /// <summary>
    /// Gets or sets the choices, paying in full first.
    /// </summary>
    public IList<InstallmentOptionViewModel> Options { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the customer already chose installments in this checkout. The plan
    /// exists, so the choice is shown rather than offered again.
    /// </summary>
    public bool IsChosen { get; set; }

    /// <summary>
    /// Gets or sets the plan the customer chose, described for them, when <see cref="IsChosen"/> is set.
    /// </summary>
    public InstallmentOptionViewModel ChosenPlan { get; set; }
}
