namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// The store's templates of its emails, of every kind (Settings › Commerce › Emails). The class keeps the name it had when
/// it served orders only, because the site stores its settings under the class name: renaming it would lose the templates
/// a store wrote and break its recipes and deployment plans.
/// </summary>
public sealed class OrderEmailTemplateSettings
{
    /// <summary>
    /// Gets or sets the HTML around every email with a kind, with <c>{{ Content }}</c> where the email goes, such as the
    /// store's logo and address; empty for none.
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// Gets or sets each email's own template, by its kind.
    /// </summary>
    public Dictionary<string, CommerceEmailTemplate> Templates { get; set; } = new(StringComparer.Ordinal);
}
