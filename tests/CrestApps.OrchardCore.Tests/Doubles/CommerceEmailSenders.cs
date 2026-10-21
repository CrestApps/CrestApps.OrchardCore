using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Email;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds the commerce email sender of a store that has written no templates of its emails: every email goes as built.
/// </summary>
public static class CommerceEmailSenders
{
    /// <summary>
    /// Returns a sender that sends through the email service the services hold, if any.
    /// </summary>
    /// <param name="services">The services.</param>
    public static CommerceEmailSender Create(IServiceProvider services)
        => new(services, new AsBuiltOrderEmailTemplates(), NullLogger<CommerceEmailSender>.Instance);

    /// <summary>
    /// Returns a sender that sends through the email service.
    /// </summary>
    /// <param name="emailService">The email service.</param>
    public static CommerceEmailSender Create(IEmailService emailService)
        => Create(new ServiceCollection().AddSingleton(emailService).BuildServiceProvider());
}
