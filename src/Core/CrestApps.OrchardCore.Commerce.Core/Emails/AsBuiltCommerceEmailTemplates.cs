using CrestApps.OrchardCore.Commerce.Emails;

namespace CrestApps.OrchardCore.Commerce.Core.Emails;

/// <summary>
/// The <see cref="ICommerceEmailTemplates"/> of a store that has written no templates: every email goes as it was built.
/// </summary>
public sealed class AsBuiltCommerceEmailTemplates : ICommerceEmailTemplates
{
    /// <inheritdoc />
    public Task<CommerceEmailMessage> ApplyAsync(CommerceEmailContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(new CommerceEmailMessage(context.Subject, context.Text, context.Html));
    }
}
