using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Shell.Configuration;
using OrchardCore.Users;
using OrchardCore.Users.Controllers;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Users;

/// <summary>
/// MD1 (fix-verification review): the handler that records a confirmation link as proof of the address lived in the
/// Customers feature, so on a site without it a link never counted. These run Orchard Core's own e-mail confirmation
/// controller over its user store on SQLite, with only the services the Email Code Sign-In feature's startup
/// registers.
/// </summary>
public sealed class EmailConfirmationLinkProofTests : IAsyncLifetime
{
    private const string Email = "pat@example.test";

    private EmailSignInHarness _harness;

    public async ValueTask InitializeAsync()
    {
        var configuration = new Mock<IShellConfiguration>();
        var empty = new ConfigurationBuilder().Build();
        configuration.Setup(c => c.GetSection(It.IsAny<string>())).Returns((string key) => empty.GetSection(key));

        _harness = await EmailSignInHarness.CreateAsync(services => new EmailCodeSignInStartup(configuration.Object).ConfigureServices(services));
    }

    public async ValueTask DisposeAsync()
        => await _harness.DisposeAsync();

    [Fact]
    public async Task AConfirmationLink_RecordsTheAddressAsProved_WithoutTheCustomersFeature()
    {
        // Arrange: an account with no password or external login, so the link is all there is to prove.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = Email, IsEnabled = true }, withPassword: false, withLogin: false);
        var link = await GenerateLinkAsync("user-1");

        // Act
        var result = await ConfirmAsync("user-1", link);

        // Assert
        Assert.IsType<ViewResult>(result);

        var account = await FindAsync("user-1");
        Assert.True(account.EmailConfirmed);
        Assert.True(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    // Owner decision 17: someone registered with the address and a password of their own, and the address's owner
    // follows the "confirm your account" mail without being signed in to that account. Orchard Core still confirms the
    // address, but nothing that belongs to it is handed over.
    [Fact]
    public async Task AConfirmationLink_ConfirmsButDoesNotProveAnAccountWithAPassword_WhenTheRequestDoesntShowIt()
    {
        // Arrange
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "squatter", Email = Email, IsEnabled = true });
        var link = await GenerateLinkAsync("user-1");

        // Act
        var result = await ConfirmAsync("user-1", link);

        // Assert
        Assert.IsType<ViewResult>(result);

        var account = await FindAsync("user-1");
        Assert.True(account.EmailConfirmed);
        Assert.False(UserEmailOwnership.IsVerified(account.Properties, account.Email));
    }

    [Fact]
    public async Task AConfirmationLink_DoesNotConfirmAnAddressTheUserChangedToAfterItWasSent()
    {
        // Arrange: the link is sent, then the address is changed directly, as Orchard Core's profile editor does.
        await _harness.CreateUserAsync(new User { UserId = "user-1", UserName = "pat", Email = "pat-old@example.test", IsEnabled = true });
        var link = await GenerateLinkAsync("user-1");

        await _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();
            var account = (User)await manager.FindByIdAsync("user-1");
            account.Email = Email;

            return await manager.UpdateAsync(account);
        });

        // Act
        var result = await ConfirmAsync("user-1", link);

        // Assert
        Assert.IsType<NotFoundResult>(result);

        var changed = await FindAsync("user-1");
        Assert.False(changed.EmailConfirmed);
        Assert.False(UserEmailOwnership.IsVerified(changed.Properties, changed.Email));
    }

    private Task<string> GenerateLinkAsync(string userId)
        => _harness.InRequestAsync(async services =>
        {
            var manager = services.GetRequiredService<UserManager<IUser>>();

            return await manager.GenerateEmailConfirmationTokenAsync(await manager.FindByIdAsync(userId));
        });

    private Task<User> FindAsync(string userId)
        => _harness.InRequestAsync(async services => (User)await services.GetRequiredService<UserManager<IUser>>().FindByIdAsync(userId));

    // One request to Orchard Core's confirmation page, as the link in the e-mail makes it.
    private Task<IActionResult> ConfirmAsync(string userId, string link)
        => _harness.InRequestAsync(services =>
        {
            var authentication = new Mock<IAuthenticationService>();
            var requestServices = new ServiceCollection()
                .AddSingleton(authentication.Object)
                .BuildServiceProvider();

            var controller = new EmailConfirmationController(
                services.GetRequiredService<UserManager<IUser>>(),
                Mock.Of<IAuthorizationService>(),
                Mock.Of<INotifier>(),
                _harness.GetUserEventHandlers(services),
                userEmailService: null,
                _harness.Cache,
                NullLogger<EmailConfirmationController>.Instance,
                Mock.Of<IHtmlLocalizer<EmailConfirmationController>>(),
                Mock.Of<IStringLocalizer<EmailConfirmationController>>());

            var httpContext = new DefaultHttpContext { RequestServices = requestServices };
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            return controller.ConfirmEmail(userId, link);
        });
}
