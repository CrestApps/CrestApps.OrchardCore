using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

public static class CreateSetupIntentEndpoint
{
    public static IEndpointRouteBuilder AddCreateSetupIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("stripe/create-setup-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(StripeConstants.RouteName.CreateSetupIntentEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSetupIntentRequest model,
        IHttpContextAccessor httpContextAccessor,
        IOptions<StripeOptions> stripeOptions,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider)
    {
        if (string.IsNullOrWhiteSpace(model.PaymentMethodId))
        {
            if (string.IsNullOrEmpty(stripeOptions.Value.ApiKey))
            {
                return TypedResults.Problem("Stripe is not configured.", instance: null, statusCode: 500);
            }

            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
        }

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

        if (httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext.User) as User;

            if (user != null)
            {
                customerOptions.Name = await displayNameProvider.GetAsync(user);
                customerOptions.Email = user?.Email;
                customerOptions.Phone = user?.PhoneNumber;
                customerOptions.InvoiceSettings.CustomFields.Add(new CustomerInvoiceSettingsCustomFieldOptions()
                {
                    Name = "UserName",
                    Value = user?.UserName ?? httpContextAccessor.HttpContext.User.Identity.Name,
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
                    Value = user?.UserName ?? httpContextAccessor.HttpContext.User.Identity.Name,
                });
            }
        }

        var stripeClient = new StripeClient(stripeOptions.Value.ApiKey);
        var customerService = new CustomerService(stripeClient);
        var customer = await customerService.CreateAsync(customerOptions);

        var setupIntentOptions = new SetupIntentCreateOptions
        {
            Customer = customer.Id,
            PaymentMethodTypes = ["card"],
        };
        var setupIntentService = new SetupIntentService(stripeClient);
        var setupIntent = await setupIntentService.CreateAsync(setupIntentOptions);

        return TypedResults.Ok(new
        {
            client_secret = setupIntent.ClientSecret,
            customer_id = customer.Id
        });
    }
}
