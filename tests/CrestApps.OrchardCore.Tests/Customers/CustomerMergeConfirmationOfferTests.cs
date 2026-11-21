using System.Reflection;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Controllers;
using CrestApps.OrchardCore.Users.Core.Services;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using ManifestFeature = OrchardCore.Modules.Manifest.FeatureAttribute;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// F2 (third fix-verification review): since owner decision 17 a confirmation link no longer proves the address of an
/// account with a password, so an ordinary registrant's guest orders stayed where they were, nothing told them so, and
/// with Email Code Sign-In off there was no "Confirm it's you" page at all, while the store still promised "Register
/// with the same email, and your orders appear in your account". A sign-in to such an account now says that guest
/// orders are waiting and links to the page, which is there whenever Customers is on.
/// </summary>
public sealed class CustomerMergeConfirmationOfferTests
{
    private const string Email = "pat@example.test";
    private const string ConfirmPath = "/sign-in/email/confirm";

    private readonly List<LocalizedHtmlString> _notices = [];
    private readonly Mock<ICustomerStore> _store = new();
    private readonly Mock<ICustomerMergeService> _merge = new();

    [Fact]
    public async Task ASignIn_ToAnAccountWhoseAddressIsntProved_SaysGuestOrdersAreWaiting_AndLinksToTheConfirmationPage()
    {
        // Arrange: a registrant with a password, and a guest order placed with their address.
        _store.Setup(store => store.ListGuestsByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([new Customer()]);

        // Act
        await CreateHandler().LoggedInAsync(new User { UserId = "user-1", Email = Email, EmailConfirmed = true, PasswordHash = "hash" });

        // Assert
        var notice = Assert.Single(_notices);
        Assert.Contains(ConfirmPath, Render(notice), StringComparison.Ordinal);
        _merge.Verify(merge => merge.MergeVerifiedGuestsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // G3 (fourth fix-verification review): the notice was shown only when guest orders with the address were waiting,
    // so registering with somebody's address told you whether they had bought there. It is now the same for every
    // user whose address isn't proved, worded conditionally, and the store isn't asked.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WithOrWithoutGuestOrders_TheSameNoticeIsShown_AndTheStoreIsNotAsked(bool guestOrdersWaiting)
    {
        // Arrange
        _store.Setup(store => store.ListGuestsByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync(guestOrdersWaiting ? [new Customer()] : []);

        // Act
        await CreateHandler().LoggedInAsync(new User { UserId = "user-1", Email = Email, EmailConfirmed = true, PasswordHash = "hash" });

        // Assert
        var notice = Assert.Single(_notices);
        Assert.StartsWith("If you placed orders as a guest", notice.Name, StringComparison.Ordinal);
        Assert.Contains(ConfirmPath, Render(notice), StringComparison.Ordinal);
        _store.Verify(store => store.ListGuestsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnceTheAddressIsProved_TheGuestsMerge_AndNothingIsSaid()
    {
        // Arrange
        var user = new User { UserId = "user-1", Email = Email, EmailConfirmed = true, PasswordHash = "hash" };
        UserEmailOwnership.MarkVerified(user.Properties, Email, DateTime.UtcNow);
        _store.Setup(store => store.ListGuestsByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([new Customer()]);

        // Act
        await CreateHandler().LoggedInAsync(user);

        // Assert
        _merge.Verify(merge => merge.MergeVerifiedGuestsAsync("user-1", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(_notices);
    }

    // Registering doesn't prove the address either, so the new registrant is told right away, not at the next sign-in.
    [Fact]
    public async Task ARegistration_WithGuestOrdersWaiting_IsToldWhereToConfirm()
    {
        _store.Setup(store => store.ListGuestsByEmailAsync(Email, It.IsAny<CancellationToken>())).ReturnsAsync([new Customer()]);

        await ActivatorUtilities.CreateInstance<CustomerMergeRegistrationFormEvent>(BuildServices())
            .RegisteredAsync(new User { UserId = "user-1", Email = Email, EmailConfirmed = true, PasswordHash = "hash" });

        Assert.Contains(ConfirmPath, Render(Assert.Single(_notices)), StringComparison.Ordinal);
    }

    [Fact]
    public void TheConfirmationPage_IsThereWheneverCustomersIs()
    {
        var customers = typeof(CustomerMergeLoginFormEvent).Assembly.GetCustomAttributes<ManifestFeature>()
            .Single(feature => feature.GetType() == typeof(ManifestFeature) && feature.Id == CustomersConstants.Features.Customers);
        var codeSignIn = typeof(EmailOwnershipController).Assembly.GetCustomAttributes<ManifestFeature>()
            .Single(feature => feature.GetType() == typeof(ManifestFeature) && feature.Id == UsersConstants.Feature.EmailCodeSignIn);

        Assert.Contains(UsersConstants.Feature.EmailOwnership, customers.Dependencies);
        Assert.Contains(UsersConstants.Feature.EmailOwnership, codeSignIn.Dependencies);
        Assert.Equal(UsersConstants.Feature.EmailOwnership, typeof(EmailOwnershipController).GetCustomAttribute<FeatureAttribute>()?.FeatureName);
    }

    // The notice as the page shows it, with its arguments.
    private static string Render(LocalizedHtmlString message)
    {
        using var writer = new StringWriter();
        message.WriteTo(writer, System.Text.Encodings.Web.HtmlEncoder.Default);

        return writer.ToString();
    }

    // The Customers feature's handler, built from the request's services as Orchard Core builds it.
    private CustomerMergeLoginFormEvent CreateHandler()
        => ActivatorUtilities.CreateInstance<CustomerMergeLoginFormEvent>(BuildServices());

    private ServiceProvider BuildServices()
    {
        var notifier = new Mock<INotifier>();
        notifier.Setup(n => n.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>()))
            .Callback((NotifyType _, LocalizedHtmlString message) => _notices.Add(message))
            .Returns(ValueTask.CompletedTask);

        var page = new Mock<IEmailOwnershipConfirmationPage>();
        page.Setup(p => p.GetUrlAsync(It.IsAny<IUser>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUser user, string _, CancellationToken _) =>
                user is User account && !UserEmailOwnership.IsVerified(account.Properties, account.Email) ? ConfirmPath : null);

        return new ServiceCollection()
            .AddLogging()
            .AddSingleton(notifier.Object)
            .AddSingleton(page.Object)
            .AddSingleton(_store.Object)
            .AddSingleton(_merge.Object)
            .AddSingleton<IHtmlLocalizer<CustomerMergeLoginFormEvent>>(new PassThroughHtmlLocalizer<CustomerMergeLoginFormEvent>())
            .BuildServiceProvider();
    }
}
