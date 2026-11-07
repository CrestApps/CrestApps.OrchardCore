using CrestApps.OrchardCore.Commerce.Core.Emails;
using CrestApps.OrchardCore.Commerce.Emails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Email;
using OrchardCore.Entities;
using OrchardCore.Liquid;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Builds the commerce email sender of a store: one that has written no templates of its emails, where every email goes
/// as built, or one with its own Liquid templates.
/// </summary>
public static class CommerceEmailSenders
{
    /// <summary>
    /// Returns a sender that sends through the email service the services hold, if any.
    /// </summary>
    /// <param name="services">The services.</param>
    public static CommerceEmailSender Create(IServiceProvider services)
        => new(services, new AsBuiltCommerceEmailTemplates(), NullLogger<CommerceEmailSender>.Instance);

    /// <summary>
    /// Returns a sender that sends through the email service.
    /// </summary>
    /// <param name="emailService">The email service.</param>
    public static CommerceEmailSender Create(IEmailService emailService)
        => Create(new ServiceCollection().AddSingleton(emailService).BuildServiceProvider());

    /// <summary>
    /// Returns a sender that sends through the email service and the store's Liquid templates.
    /// </summary>
    /// <param name="emailService">The email service.</param>
    /// <param name="settings">The store's templates.</param>
    public static CommerceEmailSender CreateWithTemplates(IEmailService emailService, OrderEmailTemplateSettings settings)
    {
        var services = new ServiceCollection().AddSingleton(emailService).BuildServiceProvider();

        return new(services, Templates(settings), NullLogger<CommerceEmailSender>.Instance);
    }

    /// <summary>
    /// Returns the store's Liquid templates, rendered with Fluid, kept under the settings' stored name.
    /// </summary>
    /// <param name="settings">The store's templates.</param>
    /// <param name="siteName">The store's name.</param>
    public static LiquidCommerceEmailTemplates Templates(OrderEmailTemplateSettings settings, string siteName = "Test store")
    {
        var site = new SiteSettings { SiteName = siteName };
        site.Put(settings);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site);

        var services = new ServiceCollection().AddSingleton(LiquidDoubles.Manager()).BuildServiceProvider();

        return new LiquidCommerceEmailTemplates(services, siteService.Object, NullLogger<LiquidCommerceEmailTemplates>.Instance);
    }
}
