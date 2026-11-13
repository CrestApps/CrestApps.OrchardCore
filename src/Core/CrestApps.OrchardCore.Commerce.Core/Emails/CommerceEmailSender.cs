using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Core.Emails;
using CrestApps.OrchardCore.Emails;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Commerce.Core.Emails;

/// <summary>
/// Sends the store's emails with the site's <see cref="TemplatedEmailSender"/>: in the email's culture, through the
/// store's templates of its emails (<see cref="ICommerceEmailTemplates"/>), and through Orchard Core's email service
/// when the Email feature is on.
/// </summary>
public sealed class CommerceEmailSender : ICommerceEmailSender
{
    private readonly TemplatedEmailSender _sender;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommerceEmailSender"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, to find the email service when the Email feature is on.</param>
    /// <param name="emailTemplates">The store's templates of its emails.</param>
    /// <param name="logger">The logger.</param>
    public CommerceEmailSender(
        IServiceProvider serviceProvider,
        ICommerceEmailTemplates emailTemplates,
        ILogger<CommerceEmailSender> logger)
    {
        _sender = new TemplatedEmailSender(serviceProvider, new StoreTemplates(emailTemplates), logger);
    }

    /// <inheritdoc/>
    public bool CanSend
        => _sender.CanSend;

    /// <inheritdoc/>
    public Task<bool> SendAsync(CommerceEmail email, Func<CancellationToken, Task<CommerceEmailContent>> buildAsync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(buildAsync);

        return _sender.SendAsync(
            new TemplatedEmail
            {
                Kind = email.Kind,
                To = email.To,
                Models = email.Models,
                Culture = email.Culture,
                Reference = email.Reference,
            },
            async token => await buildAsync(token) is { } content
                ? new TemplatedEmailContent { Subject = content.Subject, Text = content.Text, Html = content.Html, Values = content.Values }
                : null,
            cancellationToken);
    }

    private sealed class StoreTemplates : ITemplatedEmailTemplates
    {
        private readonly ICommerceEmailTemplates _templates;

        public StoreTemplates(ICommerceEmailTemplates templates)
        {
            _templates = templates;
        }

        public async Task<TemplatedEmailMessage> ApplyAsync(TemplatedEmailContext context, CancellationToken cancellationToken = default)
        {
            var message = await _templates.ApplyAsync(new CommerceEmailContext
            {
                Kind = context.Kind,
                Models = context.Models,
                Subject = context.Subject,
                Text = context.Text,
                Html = context.Html,
                Values = context.Values,
            }, cancellationToken);

            return new TemplatedEmailMessage(message.Subject, message.Text, message.Html);
        }
    }
}
