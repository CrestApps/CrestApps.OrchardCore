using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.Core.Omnichannel.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for binding the customer-record contracts to Orchard Core content.
/// </summary>
public static class OmnichannelContentServiceCollectionExtensions
{
    /// <summary>
    /// Binds the contact contracts to Orchard Core content types and content items.
    /// </summary>
    /// <remarks>
    /// Nothing about how a contact is stored changes: every admin screen, content-type editor,
    /// import and index keeps working exactly as before. The framework's contact is a projection
    /// built on read and never persisted.
    /// </remarks>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddOrchardCoreOmnichannelContacts(this IServiceCollection services)
    {
        services.TryAddScoped<IContactDefinitionProvider, ContentTypeContactDefinitionProvider>();
        services.TryAddScoped<IOmnichannelContactResolver, ContentItemOmnichannelContactResolver>();
        services.TryAddScoped<IOmnichannelContactWriter, ContentItemOmnichannelContactWriter>();

        return services;
    }

    /// <summary>
    /// Binds the subject contracts to Orchard Core content types and content items.
    /// </summary>
    /// <param name="services">The services.</param>
    public static IServiceCollection AddOrchardCoreOmnichannelSubjects(this IServiceCollection services)
    {
        services.TryAddScoped<ISubjectDefinitionProvider, ContentTypeSubjectDefinitionProvider>();
        services.TryAddScoped<IOmnichannelSubjectAccessor, ContentItemOmnichannelSubjectAccessor>();
        services.TryAddScoped<IActivitySubjectWriter, ContentItemActivitySubjectWriter>();

        return services;
    }
}
