using CrestApps.OrchardCore.ContactCenter.Maintenance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Maintenance;

/// <summary>
/// Provides registration of the Contact Center preview maintenance data sets.
/// </summary>
public static class ContactCenterPreviewMaintenanceServiceCollectionExtensions
{
    /// <summary>
    /// Registers one <see cref="IContactCenterPreviewDataSet"/> for every persisted Contact Center document
    /// type declared by <see cref="ContactCenterPreviewDataSetRegistry"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection so calls can be chained.</returns>
    public static IServiceCollection AddContactCenterPreviewDataSets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (var descriptor in ContactCenterPreviewDataSetRegistry.Descriptors)
        {
            var dataSetType = typeof(ContactCenterPreviewDataSet<>).MakeGenericType(descriptor.DocumentType);
            var captured = descriptor;

            services.AddScoped<IContactCenterPreviewDataSet>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<ContactCenterPreviewMaintenanceOptions>>();

                return (IContactCenterPreviewDataSet)Activator.CreateInstance(
                    dataSetType,
                    serviceProvider.GetRequiredService<ISession>(),
                    captured.GovernanceCategoryKey,
                    captured.IsConfiguration,
                    options.Value.PageSize);
            });
        }

        return services;
    }
}
