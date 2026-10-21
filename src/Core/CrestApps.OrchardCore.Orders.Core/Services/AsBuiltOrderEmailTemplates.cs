namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The <see cref="IOrderEmailTemplates"/> of a site where the store can't write its own templates: every email goes as
/// it was built.
/// </summary>
public sealed class AsBuiltOrderEmailTemplates : IOrderEmailTemplates
{
    /// <inheritdoc />
    public Task<OrderEmailMessage> ApplyAsync(OrderEmailContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(new OrderEmailMessage(context.Subject, context.Text, context.Html));
    }
}
