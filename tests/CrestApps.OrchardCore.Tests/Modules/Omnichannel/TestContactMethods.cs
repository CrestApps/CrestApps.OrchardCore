using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// The contact-methods services as a tenant registers them, for tests that build their subjects by hand.
/// </summary>
internal static class TestContactMethods
{
    /// <summary>
    /// The accessor that reads every contact type from its <c>ContactMethods</c> bag.
    /// </summary>
    public static IContactMethodsAccessor Accessor { get; } = CreateServices().GetRequiredService<IContactMethodsAccessor>();

    /// <summary>
    /// Reads a contact's contact methods the way the product does.
    /// </summary>
    /// <param name="contact">The contact.</param>
    public static ContactMethodSet Get(ContentItem contact)
        => Accessor.Get(contact);

    /// <summary>
    /// Builds a service provider with the contact-methods services registered, after letting the test add its own.
    /// </summary>
    /// <param name="configure">Adds the test's own registrations, such as a provider for a content type.</param>
    public static ServiceProvider CreateServices(Action<IServiceCollection> configure = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddContactMethods();
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }
}
