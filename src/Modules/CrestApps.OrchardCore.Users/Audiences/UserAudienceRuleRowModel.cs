namespace CrestApps.OrchardCore.Users.Audiences;

/// <summary>
/// One rule row of the audience editor; the template row uses the index <c>__index__</c>.
/// </summary>
public class UserAudienceRuleRowModel
{
    /// <summary>
    /// Gets or sets the index of the row in the form.
    /// </summary>
    public string Index { get; set; }

    /// <summary>
    /// Gets or sets the rule.
    /// </summary>
    public UserAudienceRuleEditViewModel Rule { get; set; }

    /// <summary>
    /// Gets or sets the kinds of rules.
    /// </summary>
    public IReadOnlyList<UserAudienceSourceViewModel> Sources { get; set; } = [];
}
