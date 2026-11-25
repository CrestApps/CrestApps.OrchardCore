namespace CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;

/// <summary>
/// One rule of the rule set editor, shown in a card of its own.
/// </summary>
public class AdherenceRuleCardViewModel
{
    /// <summary>
    /// Gets or sets the position of the rule in the posted rules.
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public AdherenceRuleEditViewModel Rule { get; set; }
}
