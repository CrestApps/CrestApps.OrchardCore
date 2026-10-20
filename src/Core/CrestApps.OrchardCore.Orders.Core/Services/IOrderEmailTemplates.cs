using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The store's own templates for the emails about orders: each email's subject and body can be written over, with the
/// email the store would send as <c>{{ Content }}</c>, and a layout around them all. Without one, an email goes as built.
/// </summary>
public interface IOrderEmailTemplates
{
    /// <summary>
    /// Returns the email to send: the built one, or the store's template of it.
    /// </summary>
    Task<OrderEmailMessage> ApplyAsync(OrderEmailContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// An email about an order as it was built, and what its template can use.
/// </summary>
public sealed class OrderEmailContext
{
    /// <summary>
    /// Gets or sets which email it is, one of <see cref="OrderEmailKinds"/>.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets or sets the order it's about, or <see langword="null"/>, such as for a gift card sent on its own.
    /// </summary>
    public Order Order { get; init; }

    public string Subject { get; init; }

    public string Text { get; init; }

    public string Html { get; init; }

    /// <summary>
    /// Gets or sets the email's own values, such as the link to pay, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}

public sealed record OrderEmailMessage(string Subject, string Text, string Html);

/// <summary>
/// The emails about orders a store can write its own templates of.
/// </summary>
public static class OrderEmailKinds
{
    public const string Confirmation = "order-confirmation";

    public const string Invoice = "order-invoice";

    public const string Quote = "order-quote";

    public const string Shipped = "order-shipped";

    public const string Return = "order-return";

    public const string GiftCard = "gift-card";

    public const string Recovery = "checkout-recovery";

    public static readonly IReadOnlyList<string> All = [Confirmation, Invoice, Quote, Shipped, Return, GiftCard, Recovery];
}

public static class OrderEmailTemplateExtensions
{
    /// <summary>
    /// Returns an email as the store's template of it has it, or as built when there's none.
    /// </summary>
    public static async Task<OrderEmailMessage> ApplyOrderEmailTemplateAsync(
        this IServiceProvider services,
        string kind,
        Order order,
        string subject,
        string text,
        string html,
        IReadOnlyDictionary<string, string> values = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.GetService<IOrderEmailTemplates>() is not { } templates)
        {
            return new OrderEmailMessage(subject, text, html);
        }

        return await templates.ApplyAsync(new OrderEmailContext
        {
            Kind = kind,
            Order = order,
            Subject = subject,
            Text = text,
            Html = html,
            Values = values ?? new Dictionary<string, string>(),
        }, cancellationToken);
    }
}
