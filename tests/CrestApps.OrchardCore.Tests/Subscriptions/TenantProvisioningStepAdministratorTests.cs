using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Drivers;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// H1 (final security review): the new-site step checked the administrator password only for "not empty" and "matches",
/// so a password the identity rules refuse, such as <c>a</c>, was paid for and then failed Orchard's setup. The step now
/// holds the administrator to the same rules, through ASP.NET Core Identity's own validators.
/// </summary>
public sealed class TenantProvisioningStepAdministratorTests
{
    [Theory]
    [InlineData("siteadmin", "admin@example.com", "a", nameof(TenantProvisioningStepViewModel.AdminPassword))]
    [InlineData("site admin!", "admin@example.com", "Password1!", nameof(TenantProvisioningStepViewModel.AdminUsername))]
    [InlineData("siteadmin", "not an address", "Password1!", nameof(TenantProvisioningStepViewModel.AdminEmail))]
    public async Task UpdateAsync_AnAdministratorTheIdentityRulesRefuse_IsRefused(string userName, string email, string password, string field)
    {
        var modelState = await PostAsync(userName, email, password);

        Assert.False(modelState.IsValid);
        Assert.Contains(modelState, entry => entry.Key.EndsWith(field, StringComparison.Ordinal) && entry.Value.Errors.Count > 0);
    }

    [Fact]
    public async Task UpdateAsync_AnAdministratorTheRulesAccept_IsSaved()
    {
        var modelState = await PostAsync("siteadmin", "admin@example.com", "Password1!");

        Assert.True(modelState.IsValid, string.Join("; ", modelState.SelectMany(entry => entry.Value.Errors).Select(error => error.ErrorMessage)));
    }

    private static async Task<ModelStateDictionary> PostAsync(string userName, string email, string password)
    {
        var session = new CheckoutSession { SessionId = "session-1", CurrentStep = SubscriptionConstants.StepKey.TenantProvisioning };
        var step = new CheckoutFlowStep { Key = SubscriptionConstants.StepKey.TenantProvisioning, CollectData = true };
        step.Data["RecipeName"] = "Blog";
        step.Data["FeatureProfile"] = "sold";
        session.Steps.Add(step);

        var shellHost = new Mock<IShellHost>();
        shellHost.Setup(host => host.GetAllSettings()).Returns([new ShellSettings { Name = ShellSettings.DefaultShellName }]);

        var catalog = new Mock<ITenantFeatureProfileCatalog>();
        catalog.Setup(value => value.GetProfileIdsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(["sold"]);

        var siteService = SiteServiceFactory.Create(new SubscriptionOnboardingSettings { AllowPrefixOnStoreHost = true });
        var rules = new TenantPurchaseRulesProvider(siteService, shellHost.Object, new HttpContextAccessor(), Options.Create(new AdminOptions()), catalog.Object, Mock.Of<ICheckoutSessionStore>(), StoreRoutes.Create());

        var driver = new TenantProvisioningStepDisplayDriver(
            shellHost.Object,
            siteService,
            rules,
            new EphemeralDataProtectionProvider(),
            Options.Create(new DocumentJsonSerializerOptions()),
            CreateUserManager(),
            new KeyLocalizer());

        var updater = new PostedUpdater(new TenantProvisioningStepViewModel
        {
            TenantName = "Shop1",
            AdminUsername = userName,
            AdminEmail = email,
            AdminPassword = password,
            ConfirmPassword = password,
        });

        await driver.UpdateAsync(new CheckoutFlow(session), new UpdateEditorContext(Mock.Of<IShape>(), string.Empty, false, string.Empty, Mock.Of<IShapeFactory>(), null, updater));

        return updater.ModelState;
    }

    // ASP.NET Core Identity's own user manager and password validator, with the default identity options a new Orchard
    // site is set up with.
    private static UserManager<IUser> CreateUserManager()
        => new(
            Mock.Of<IUserStore<IUser>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<IPasswordHasher<IUser>>(),
            [],
            [new PasswordValidator<IUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<IUser>>.Instance);

    private sealed class KeyLocalizer : IStringLocalizer<TenantProvisioningStepDisplayDriver>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class PostedUpdater : IUpdateModel
    {
        private readonly TenantProvisioningStepViewModel _posted;

        public PostedUpdater(TenantProvisioningStepViewModel posted)
        {
            _posted = posted;
        }

        public ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model) where TModel : class => TryUpdateModelAsync(model, string.Empty);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix) where TModel : class
        {
            if (model is TenantProvisioningStepViewModel target)
            {
                foreach (var property in typeof(TenantProvisioningStepViewModel).GetProperties().Where(property => property.CanRead && property.CanWrite))
                {
                    property.SetValue(target, property.GetValue(_posted));
                }
            }

            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params System.Linq.Expressions.Expression<Func<TModel, object>>[] includeExpressions) where TModel : class
            => TryUpdateModelAsync(model, prefix);

        public bool TryValidateModel(object model) => true;

        public bool TryValidateModel(object model, string prefix) => true;
    }
}
