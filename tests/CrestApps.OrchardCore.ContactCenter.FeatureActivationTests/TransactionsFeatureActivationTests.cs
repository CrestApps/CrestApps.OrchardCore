using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Transactions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.FeatureActivationTests;

/// <summary>
/// Transactions runs on its own, without Checkout or the user display-name feature. On a tenant with Transactions
/// alone, the transaction list could not resolve the display-name provider and the payment and refund ledgers could
/// not resolve Checkout's stores, so each of those pages answered 500.
/// </summary>
public sealed class TransactionsFeatureActivationTests
{
    private const string PaymentsAdminController = "CrestApps.OrchardCore.Transactions.Controllers.PaymentsAdminController";
    private const string RefundsAdminController = "CrestApps.OrchardCore.Transactions.Controllers.RefundsAdminController";

    [Fact]
    public async Task WithTransactionsAlone_EveryRoutedControllerIsConstructed_AndTheLedgersAreAbsent()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile("transactions-alone", TransactionsConstants.Features.Area));

        // Act
        var controllers = await host.ExecuteInTenantScopeAsync(tenant, services => Task.FromResult(ConstructRoutedControllers(services)));

        // Assert
        Assert.Contains("CrestApps.OrchardCore.Transactions.Controllers.AdminController", controllers);
        Assert.DoesNotContain(PaymentsAdminController, controllers);
        Assert.DoesNotContain(RefundsAdminController, controllers);
    }

    [Fact]
    public async Task WithCheckout_ThePaymentAndRefundLedgersAreRouted_AndConstructed()
    {
        // Arrange
        await using var host = await ContactCenterFeatureActivationHost.StartAsync();
        var tenant = await host.CreateTenantAsync(Profile(
            "transactions-with-checkout",
            TransactionsConstants.Features.Area,
            CheckoutConstants.Features.Area));

        // Act
        var controllers = await host.ExecuteInTenantScopeAsync(tenant, services => Task.FromResult(ConstructRoutedControllers(services)));

        // Assert
        Assert.Contains(PaymentsAdminController, controllers);
        Assert.Contains(RefundsAdminController, controllers);
    }

    /// <summary>
    /// Constructs every Transactions controller the tenant routes, the way a request would, so a constructor
    /// dependency no enabled feature registers fails here rather than as a 500 on the page.
    /// </summary>
    /// <param name="services">The tenant service provider.</param>
    /// <returns>The name of each routed Transactions controller.</returns>
    private static List<string> ConstructRoutedControllers(IServiceProvider services)
    {
        var controllerTypes = services.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .Select(descriptor => descriptor.ControllerTypeInfo.AsType())
            .Where(type => type.Assembly == typeof(Transactions.Startup).Assembly)
            .Distinct()
            .ToArray();

        foreach (var controllerType in controllerTypes)
        {
            Assert.NotNull(ActivatorUtilities.CreateInstance(services, controllerType));
        }

        return [.. controllerTypes.Select(type => type.FullName)];
    }

    private static ContactCenterTenantProfile Profile(string id, params string[] features)
        => new()
        {
            Id = id,
            ProviderProfile = "none",
            Features = features,
        };
}
