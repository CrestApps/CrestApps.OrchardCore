using System.Text.Json;
using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;
using OrchardCore.Json;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Stripe.Endpoints;

public static class CreateSetupIntentEndpoint
{
    public static IEndpointRouteBuilder AddStripeCreateSetupIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-setup-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreateSetupIntentEndpoint)
            .DisableAntiforgery();

        return builder;
    }

    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSetupIntentPayment model,
        IOptions<StripeOptions> stripeOptions,
        ISubscriptionSessionStore subscriptionSessionStore,
        IHttpContextAccessor httpContextAccessor,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStripeCustomerService stripeCustomerService,
        IStripeSetupIntentService stripeSetupIntentService)
    {
        if (string.IsNullOrEmpty(stripeOptions.Value.ApiKey))
        {
            return TypedResults.Problem("Stripe is not configured.", instance: null, statusCode: 500);
        }

        if (string.IsNullOrWhiteSpace(model.PaymentMethodId) || string.IsNullOrWhiteSpace(model.SessionId))
        {
            return TypedResults.BadRequest(new
            {
                ErrorMessage = "Invalid request data",
                ErrorCode = 1,
            });
        }

        var session = await subscriptionSessionStore.GetAsync(model.SessionId, SubscriptionSessionStatus.Pending);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        var invoice = session.As<Invoice>();

        if (invoice == null)
        {
            return TypedResults.NotFound();
        }

        var customerRequest = new CreateCustomerRequest()
        {
            PaymentMethodId = model.PaymentMethodId,
            Metadata = model.Metadata ?? [],
        };

        customerRequest.Metadata["sessionId"] = model.SessionId;

        if (httpContextAccessor.HttpContext.User.Identity.IsAuthenticated)
        {
            var user = await userManager.GetUserAsync(httpContextAccessor.HttpContext.User) as User;

            if (user != null)
            {
                await SetCustomerInfoAsync(customerRequest, user, displayNameProvider);
            }
            else
            {
                customerRequest.Metadata["userName"] = httpContextAccessor.HttpContext.User.Identity.Name;
            }
        }
        else if (session.SavedSteps.TryGetPropertyValue(SubscriptionConstants.StepKey.UserRegistration, out var node))
        {
            // If the subscriber is a new user, try to get their info from the session.
            var registrationStep = node.Deserialize<UserRegistrationStep>(documentJsonSerializerOptions.Value.SerializerOptions);

            if (!registrationStep.IsGuest)
            {
                await SetCustomerInfoAsync(customerRequest, registrationStep.User, displayNameProvider);
            }
            else
            {
                customerRequest.Metadata.Add("userId", "guest");
            }
        }

        var customerResult = await stripeCustomerService.CreateAsync(customerRequest);

        if (customerResult == null)
        {
            return TypedResults.Problem("Unable to create a customer.", instance: null, statusCode: 500);
        }

        var intentRequest = new CreateSetupIntentRequest
        {
            PaymentMethodId = model.PaymentMethodId,
            CustomerId = customerResult.CustomerId,
            Metadata = model.Metadata ?? [],
        };
        intentRequest.Metadata["sessionId"] = model.SessionId;

        var result = await stripeSetupIntentService.CreateAsync(intentRequest);

        session.Put(new StripeSetupIntentMetadata
        {
            PaymentMethodId = model.PaymentMethodId,
            CustomerId = customerResult.CustomerId,
        });

        await subscriptionSessionStore.SaveAsync(session);

        return TypedResults.Ok(new
        {
            clientSecret = result.ClientSecret,
            customerId = customerResult.CustomerId,
            status = result.Status,
            processInitialPayment = invoice.DueNow > GetMinimumAllowed(invoice),
        });
    }

    private static async Task SetCustomerInfoAsync(CreateCustomerRequest customerRequest, User user, IDisplayNameProvider displayNameProvider)
    {
        customerRequest.Name = await displayNameProvider.GetAsync(user);
        customerRequest.Email = user.Email;
        customerRequest.Phone = user.PhoneNumber;
        customerRequest.Metadata["userName"] = user.UserName;
        customerRequest.Metadata["userId"] = user.UserId;
    }

    private static double GetMinimumAllowed(Invoice invoice)
    {
        if (StripeLimits.TryGetStripePaymentLimit(invoice.Currency, out var limits))
        {
            return limits?.Minimum ?? 0;
        }

        return 0;
    }
}
