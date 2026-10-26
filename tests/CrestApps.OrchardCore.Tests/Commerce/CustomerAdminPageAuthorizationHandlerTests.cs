using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.Admin;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using SubscriptionControllers = CrestApps.OrchardCore.Subscriptions.Controllers;
using TransactionControllers = CrestApps.OrchardCore.Transactions.Controllers;

namespace CrestApps.OrchardCore.Tests.Commerce;

public sealed class CustomerAdminPageAuthorizationHandlerTests
{
    [Fact]
    public async Task GrantsAdminAccess_OnACustomerPage_ToASignedInUser()
    {
        // Arrange
        var context = CreateContext(AdminPermissions.AccessAdminPanel, SignedIn());

        // Act
        await CreateHandler(new CustomerAdminPageAttribute()).HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task DoesNotGrantAdminAccess_OnAnyOtherAdminPage()
    {
        // Arrange: the dashboard, or a staff page, has no marker.
        var context = CreateContext(AdminPermissions.AccessAdminPanel, SignedIn());

        // Act
        await CreateHandler().HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task DoesNotGrantAdminAccess_ToAnAnonymousVisitor()
    {
        // Arrange: an anonymous visitor is sent to sign in instead.
        var context = CreateContext(AdminPermissions.AccessAdminPanel, new ClaimsPrincipal(new ClaimsIdentity()));

        // Act
        await CreateHandler(new CustomerAdminPageAttribute()).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task DoesNotGrantAnyOtherPermission_OnACustomerPage()
    {
        // Arrange: the page's own permission is still checked as usual.
        var context = CreateContext(new Permission("ManageTransactions"), SignedIn());

        // Act
        await CreateHandler(new CustomerAdminPageAttribute()).HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task DoesNotGrantAdminAccess_OutsideARequest()
    {
        // Arrange
        var context = CreateContext(AdminPermissions.AccessAdminPanel, SignedIn());
        var handler = new CustomerAdminPageAuthorizationHandler(new HttpContextAccessor());

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Theory]
    [InlineData(typeof(SubscriptionControllers.MySubscriptionsController))]
    [InlineData(typeof(SubscriptionControllers.MySitesController))]
    [InlineData(typeof(TransactionControllers.TransactionController))]
    [InlineData(typeof(TransactionControllers.TransactionInvoiceController))]
    [InlineData(typeof(TransactionControllers.TransactionReceiptController))]
    public void CustomerPages_AreMarked(Type controller)
        => Assert.NotNull(controller.GetCustomAttribute<CustomerAdminPageAttribute>());

    [Theory]
    [InlineData(typeof(SubscriptionControllers.TenantProvisioningAdminController))]
    [InlineData(typeof(SubscriptionControllers.SubscriptionAgreementsController))]
    [InlineData(typeof(SubscriptionControllers.InstallmentPlansController))]
    [InlineData(typeof(TransactionControllers.AdminController))]
    [InlineData(typeof(TransactionControllers.PaymentsAdminController))]
    [InlineData(typeof(TransactionControllers.RefundsAdminController))]
    public void StaffPages_AreNotMarked(Type controller)
        => Assert.Null(controller.GetCustomAttribute<CustomerAdminPageAttribute>());

    private static CustomerAdminPageAuthorizationHandler CreateHandler(params object[] endpointMetadata)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(endpointMetadata), "Test endpoint"));

        return new CustomerAdminPageAuthorizationHandler(new HttpContextAccessor { HttpContext = httpContext });
    }

    private static AuthorizationHandlerContext CreateContext(Permission permission, ClaimsPrincipal user)
        => new([new PermissionRequirement(permission)], user, resource: null);

    private static ClaimsPrincipal SignedIn()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "customer-1")], "Test"));
}
