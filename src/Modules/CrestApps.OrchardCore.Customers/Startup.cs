using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Indexes;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Migrations;
using CrestApps.OrchardCore.Customers.Navigation;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Customers;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Customers live in their own YesSql collection, which has to be declared for its documents to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CustomersConstants.CollectionName));
        services.AddDataMigration<CustomerMigrations>()
            .AddIndexProvider<CustomerIndexProvider>();

        services.AddScoped<ICustomerStore, CustomerStore>();
        services.AddScoped<ICustomerService, DefaultCustomerService>();

        // Statements, receipts and reminders address a customer by their customer record once there is one.
        services.AddScoped<DefaultCustomerContactResolver>();
        services.Replace(ServiceDescriptor.Scoped<ICustomerContactResolver, CustomerRecordContactResolver>());

        services.AddScoped<IPermissionProvider, CustomerPermissionsProvider>();
        services.AddNavigationProvider<CustomersAdminMenu>();
    }
}
