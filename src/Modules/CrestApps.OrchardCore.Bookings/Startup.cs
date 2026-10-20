using CrestApps.OrchardCore.Bookings.BackgroundTasks;
using CrestApps.OrchardCore.Bookings.Drivers;
using CrestApps.OrchardCore.Bookings.Handlers;
using CrestApps.OrchardCore.Bookings.Indexes;
using CrestApps.OrchardCore.Bookings.Migrations;
using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Bookings.Navigation;
using CrestApps.OrchardCore.Bookings.Recipes;
using CrestApps.OrchardCore.Bookings.Services;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Bookings;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Dates, bookings and balances live in their own YesSql collection, which has to be declared to be stored.
        services.Configure<StoreCollectionOptions>(options => options.Collections.Add(BookingsConstants.CollectionName));
        services.AddDataMigration<BookingMigrations>()
            .AddIndexProvider<BookingSlotIndexProvider>()
            .AddIndexProvider<BookingIndexProvider>()
            .AddIndexProvider<BookingBalanceIndexProvider>();
        services.AddScoped<IBookingSlotStore, BookingSlotStore>();
        services.AddScoped<IBookingStore, BookingStore>();
        services.AddScoped<IBookingBalanceStore, BookingBalanceStore>();
        services.AddScoped<BookingCapacityService>();
        services.AddScoped<BookingService>();
        services.AddScoped<BookingEmailSender>();
        services.AddScoped<BookingDepositService>();
        services.AddScoped<BookingBalanceCharger>();

        // A booking's date and option ride on its cart line, and its order line books it.
        services.AddScoped<ICartLineHandler, BookingCartLineHandler>();
        services.AddScoped<IOrderEventHandler, BookingOrderEventHandler>();
        services.AddScoped<ICartActionProvider, BookingCartActionProvider>();
        services.Configure<CartOptions>(options => options.ChooseOnProductPageParts.Add(nameof(BookingPart)));

        // A deposit now, and the rest owed and charged to the same card later.
        services.AddScoped<ICheckoutTenderProvider, BookingBalanceTenderProvider>();
        services.AddDisplayDriver<CheckoutFlow, BookingDepositCheckoutFlowDisplayDriver>();
        services.AddDisplayDriver<CheckoutFlow, BookingTravelersCheckoutFlowDisplayDriver>();

        services.AddContentPart<BookingPart>()
            .UseDisplayDriver<BookingPartDisplayDriver>();
        services.AddDisplayDriver<Booking, BookingDisplayDriver>();

        services.AddSingleton<IBackgroundTask, BookingsBackgroundTask>();
        services.AddPermissionProvider<BookingsPermissionProvider>();
        services.AddNavigationProvider<BookingsAdminMenu>();
    }
}

/// <summary>
/// Adds dates to booking products from a recipe, such as a sample tour's departures.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
        => services.AddRecipeExecutionStep<BookingDatesStep>();
}
