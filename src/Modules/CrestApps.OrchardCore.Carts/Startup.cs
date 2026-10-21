using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Indexes;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Carts.Drivers;
using CrestApps.OrchardCore.Carts.Handlers;
using CrestApps.OrchardCore.Carts.Migrations;
using CrestApps.OrchardCore.Carts.Services;
using CrestApps.OrchardCore.Carts.Tasks;
using CrestApps.OrchardCore.Carts.ViewModels;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Admin.Models;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;
using OrchardCore.Users.Events;

namespace CrestApps.OrchardCore.Carts;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Carts live in their own YesSql collection, which has to be declared for its documents to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(CartConstants.CollectionName));
        services.AddDataMigration<CartMigrations>()
            .AddIndexProvider<CartIndexProvider>();

        services.AddOptions<CartOptions>();
        services.TryAddScoped<GuestSessionTokenManager>();
        services.AddScoped<ICartStore, CartStore>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<ExpiredCartCleaner>();
        services.AddSingleton<IBackgroundTask, ExpiredCartsBackgroundTask>();
        services.AddScoped<ILoginFormEvent, CartLoginFormEvent>();

        services.AddContentPart<ProductPart>()
            .UseDisplayDriver<AddToCartDisplayDriver>()
            .UseDisplayDriver<GroupedProductsDisplayDriver>();

        services.AddDisplayDriver<Navbar, CartNavbarDisplayDriver>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, CartResourceManagementOptionsConfiguration>();

        // The cart's shapes can be overridden in Liquid as well as Razor.
        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<AddToCartViewModel>();
            options.MemberAccessStrategy.Register<CartViewModel>();
            options.MemberAccessStrategy.Register<CartLinkViewModel>();
            options.MemberAccessStrategy.Register<CartPricing>();
            options.MemberAccessStrategy.Register<CartLinePricing>();
            options.MemberAccessStrategy.Register<Cart>();
            options.MemberAccessStrategy.Register<CartLine>();
        });
    }
}
