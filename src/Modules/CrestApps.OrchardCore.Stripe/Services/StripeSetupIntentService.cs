using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripeSetupIntentService : IStripeSetupIntentService
{
    private readonly StripeClient _stripeClient;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StripeSetupIntentService(
        StripeClient stripeClient,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _stripeClient = stripeClient;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<CreateSetupIntentResponse> CreateAsync(CreateSetupIntentRequest model)
    {
        var customerOptions = new CustomerCreateOptions
        {
            PaymentMethod = model.PaymentMethodId,
            InvoiceSettings = new CustomerInvoiceSettingsOptions
            {
                DefaultPaymentMethod = model.PaymentMethodId,
                CustomFields = [],
            },
            Metadata = model.Metadata,
        };

        if (_httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext.User) as User;

            if (user != null)
            {
                customerOptions.Name = await _displayNameProvider.GetAsync(user);
                customerOptions.Email = user?.Email;
                customerOptions.Phone = user?.PhoneNumber;
                customerOptions.InvoiceSettings.CustomFields.Add(new CustomerInvoiceSettingsCustomFieldOptions()
                {
                    Name = "UserName",
                    Value = user?.UserName ?? _httpContextAccessor.HttpContext.User.Identity.Name,
                });
                customerOptions.InvoiceSettings.CustomFields.Add(new CustomerInvoiceSettingsCustomFieldOptions()
                {
                    Name = "UserId",
                    Value = user?.UserId,
                });
            }
            else
            {
                customerOptions.InvoiceSettings.CustomFields.Add(new CustomerInvoiceSettingsCustomFieldOptions()
                {
                    Name = "UserName",
                    Value = user?.UserName ?? _httpContextAccessor.HttpContext.User.Identity.Name,
                });
            }
        }

        var customerService = new CustomerService(_stripeClient);
        var customer = await customerService.CreateAsync(customerOptions);

        var setupIntentOptions = new SetupIntentCreateOptions
        {
            Customer = customer.Id,
            PaymentMethod = model.PaymentMethodId,
            PaymentMethodTypes = ["card"],
            Metadata = customer.Metadata,
        };

        var setupIntentService = new SetupIntentService(_stripeClient);

        var setupIntent = await setupIntentService.CreateAsync(setupIntentOptions);

        return new CreateSetupIntentResponse()
        {
            Status = setupIntent.Status,
            ClientSecret = setupIntent.ClientSecret,
            CustomerId = customer.Id,
        };
    }
}
