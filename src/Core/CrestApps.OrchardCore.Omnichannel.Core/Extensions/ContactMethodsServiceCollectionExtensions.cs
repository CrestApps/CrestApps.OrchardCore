using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering how contacts' phone numbers and email addresses are read and written.
/// </summary>
public static class ContactMethodsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services that read and write contacts' phone numbers and email addresses. Every feature that
    /// uses them calls it; registering it more than once has no effect.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddContactMethods(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<BagContactMethodsProvider>();
        services.TryAddSingleton<IContactMethodsAccessor, DefaultContactMethodsAccessor>();
        services.TryAddScoped<BagContactMethodsWriter>();
        services.TryAddScoped<IContactMethodsWriter, DefaultContactMethodsWriter>();

        return services;
    }

    /// <summary>
    /// Makes contacts of the given content types read their phone numbers and email addresses through a provider
    /// instead of the <c>ContactMethods</c> bag. Those types are not given the bag, and the contact import leaves
    /// its email and phone columns to the types' own fields.
    /// </summary>
    /// <remarks>
    /// The provider is registered as a singleton, and it must read only what is stored on the content item it is
    /// given; see <see cref="IContactMethodsProvider"/>. A type that already has a bag keeps it and its data: the bag
    /// is no longer read, and it is never removed.
    /// </remarks>
    /// <typeparam name="TProvider">The provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="contentTypes">The content types the provider reads.</param>
    public static IServiceCollection AddContactMethodsProvider<TProvider>(this IServiceCollection services, params string[] contentTypes)
        where TProvider : class, IContactMethodsProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureContentTypes(contentTypes);

        services.AddContactMethods();
        services.TryAddSingleton<TProvider>();
        services.Configure<ContactMethodsOptions>(options =>
        {
            foreach (var contentType in contentTypes)
            {
                options.Providers[contentType] = typeof(TProvider);
            }
        });

        return services;
    }

    /// <summary>
    /// Makes the phone numbers and email addresses the product learns about contacts of the given content types (an
    /// email address given in a conversation, the numbers of a converted lead) be written by a writer of the type's
    /// own, for a type that reads them through a provider registered with <c>AddContactMethodsProvider</c>.
    /// Without a writer such a type is read-only, and nothing is written over the values it keeps.
    /// </summary>
    /// <remarks>
    /// The writer is registered as a scoped service. It changes the content item only; the caller saves it.
    /// </remarks>
    /// <typeparam name="TWriter">The writer.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="contentTypes">The content types the writer writes.</param>
    public static IServiceCollection AddContactMethodsWriter<TWriter>(this IServiceCollection services, params string[] contentTypes)
        where TWriter : class, IContactMethodsWriter
    {
        ArgumentNullException.ThrowIfNull(services);
        EnsureContentTypes(contentTypes);

        services.AddContactMethods();
        services.TryAddScoped<TWriter>();
        services.Configure<ContactMethodsOptions>(options =>
        {
            foreach (var contentType in contentTypes)
            {
                options.Writers[contentType] = typeof(TWriter);
            }
        });

        return services;
    }

    private static void EnsureContentTypes(string[] contentTypes)
    {
        ArgumentNullException.ThrowIfNull(contentTypes);

        if (contentTypes.Length == 0)
        {
            throw new ArgumentException("At least one content type is required.", nameof(contentTypes));
        }

        foreach (var contentType in contentTypes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(contentType, nameof(contentTypes));
        }
    }
}
