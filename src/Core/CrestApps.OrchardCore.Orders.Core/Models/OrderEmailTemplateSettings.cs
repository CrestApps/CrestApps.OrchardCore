namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// The store's templates for its emails about orders (Settings › Commerce › Emails).
/// </summary>
public sealed class OrderEmailTemplateSettings
{
    /// <summary>
    /// Gets or sets the HTML around every email about an order, with <c>{{ Content }}</c> where the email goes, such as
    /// the store's logo and address; empty for none.
    /// </summary>
    public string Layout { get; set; }

    /// <summary>
    /// Gets or sets each email's own template, by its kind (<see cref="Services.OrderEmailKinds"/>).
    /// </summary>
    public Dictionary<string, OrderEmailTemplate> Templates { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// A store's template of one email: a subject and a body with Liquid, used when it's on.
/// </summary>
public sealed class OrderEmailTemplate
{
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the subject, with Liquid; the email's own when empty.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the body, as HTML with Liquid; <c>{{ Content }}</c> is the email as the store would send it.
    /// </summary>
    public string Body { get; set; }
}
