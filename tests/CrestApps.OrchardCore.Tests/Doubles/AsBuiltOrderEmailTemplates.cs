using CrestApps.OrchardCore.Orders.Core.Services;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Order email templates for a store that has written none: every email goes as it was built.
/// </summary>
public sealed class AsBuiltOrderEmailTemplates : IOrderEmailTemplates
{
    /// <inheritdoc />
    public Task<OrderEmailMessage> ApplyAsync(OrderEmailContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new OrderEmailMessage(context.Subject, context.Text, context.Html));
}
