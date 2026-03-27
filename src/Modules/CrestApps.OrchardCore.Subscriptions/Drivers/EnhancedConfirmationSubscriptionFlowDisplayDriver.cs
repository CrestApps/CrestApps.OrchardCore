using CrestApps.OrchardCore.Payments.Core.Models;
using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Enhances the subscription confirmation view with detailed information.
/// </summary>
public class EnhancedConfirmationSubscriptionFlowDisplayDriver : DisplayDriver<SubscriptionFlow>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IContentManager _contentManager;
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly ISiteService _siteService;

    public EnhancedConfirmationSubscriptionFlowDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IContentManager contentManager,
        IClock clock,
        ILocalClock localClock,
        ISiteService siteService)
    {
        _httpContextAccessor = httpContextAccessor;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _contentManager = contentManager;
        _clock = clock;
        _localClock = localClock;
        _siteService = siteService;
    }

    public override async Task<IDisplayResult> DisplayAsync(SubscriptionFlow model, BuildDisplayContext context)
    {
        if (!context.DisplayType.Equals("Confirmation", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext.User);
        var contentItem = await _contentManager.GetAsync(model.Session.ContentItemId);
        
        if (contentItem == null)
        {
            return null;
        }

        var subscriptionPart = contentItem.As<SubscriptionPart>();
        var productPart = contentItem.As<ProductPart>();
        var subscriptionsMetadata = model.Session.As<SubscriptionsMetadata>();
        var site = await _siteService.GetSiteSettingsAsync();

        if (subscriptionPart == null || productPart == null)
        {
            return null;
        }

        var viewModel = new SubscriptionConfirmationViewModel
        {
            SessionId = model.Session.SessionId,
            ServicePlanTitle = contentItem.DisplayText,
            OwnerName = user != null ? await _displayNameProvider.GetAsync(user) : model.Session.OwnerName,
            OwnerEmail = (user as User)?.Email,
            SubscriptionAmount = productPart.Price,
            BillingDuration = GetBillingDurationText(subscriptionPart),
            StartDate = (await _localClock.ConvertToLocalAsync(_clock.UtcNow)).DateTime,
            ManagementUrl = $"{_httpContextAccessor.HttpContext.Request.Scheme}://{_httpContextAccessor.HttpContext.Request.Host}/subscription-dashboard",
            SupportEmail = site.As<SubscriptionSettings>()?.SupportEmail,
        };

        // Get subscription details
        if (subscriptionsMetadata?.Subscriptions != null && subscriptionsMetadata.Subscriptions.Count > 0)
        {
            var subscription = subscriptionsMetadata.Subscriptions.First();
            
            if (subscription.ExpiresAt.HasValue)
            {
                viewModel.NextPaymentDate = (await _localClock.ConvertToLocalAsync(subscription.ExpiresAt.Value)).DateTime;
            }

            if (subscription.PaymentMethod != null)
            {
                viewModel.PaymentMethod = subscription.PaymentMethod.Type;
            }
        }

        // Check for initial payment
        if (subscriptionPart.InitialAmount.HasValue && subscriptionPart.InitialAmount.Value > 0)
        {
            viewModel.InitialPaymentAmount = subscriptionPart.InitialAmount.Value;
            viewModel.InitialPaymentDescription = subscriptionPart.InitialAmountDescription;
        }

        // Check for tenant onboarding information
        var tenantOnboardingPart = contentItem.As<TenantOnboardingPart>();
        if (tenantOnboardingPart != null)
        {
            var tenantMetadata = model.Session.As<TenantOnboardingMetadata>();
            if (tenantMetadata != null)
            {
                viewModel.TenantName = tenantMetadata.TenantName;
                viewModel.TenantUrl = tenantMetadata.RequestUrlPrefix;
                viewModel.SiteAdminUsername = tenantMetadata.AdminUsername;
                viewModel.SiteAdminEmail = tenantMetadata.AdminEmail;
            }
        }

        return Initialize<SubscriptionConfirmationViewModel>("EnhancedSubscriptionConfirmation", vm =>
        {
            vm.SessionId = viewModel.SessionId;
            vm.ServicePlanTitle = viewModel.ServicePlanTitle;
            vm.OwnerName = viewModel.OwnerName;
            vm.OwnerEmail = viewModel.OwnerEmail;
            vm.SubscriptionAmount = viewModel.SubscriptionAmount;
            vm.BillingDuration = viewModel.BillingDuration;
            vm.StartDate = viewModel.StartDate;
            vm.NextPaymentDate = viewModel.NextPaymentDate;
            vm.PaymentMethod = viewModel.PaymentMethod;
            vm.InitialPaymentAmount = viewModel.InitialPaymentAmount;
            vm.InitialPaymentDescription = viewModel.InitialPaymentDescription;
            vm.TenantName = viewModel.TenantName;
            vm.TenantUrl = viewModel.TenantUrl;
            vm.SiteAdminUsername = viewModel.SiteAdminUsername;
            vm.SiteAdminEmail = viewModel.SiteAdminEmail;
            vm.ManagementUrl = viewModel.ManagementUrl;
            vm.SupportEmail = viewModel.SupportEmail;
        }).Location("Confirmation", "Details:10");
    }

    private static string GetBillingDurationText(SubscriptionPart subscriptionPart)
    {
        var duration = subscriptionPart.BillingDuration;
        var type = subscriptionPart.DurationType.ToString().ToLowerInvariant();
        
        if (duration == 1)
        {
            // Singular form
            type = type.TrimEnd('s');
        }
        
        return $"{duration} {type}";
    }
}
