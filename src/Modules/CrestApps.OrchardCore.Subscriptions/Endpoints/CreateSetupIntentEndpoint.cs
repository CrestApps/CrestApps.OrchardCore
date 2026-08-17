using System.Text.Json;
using CrestApps.OrchardCore.Checkout.Services;
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
using OrchardCore.RateLimits;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// Registers the Stripe setup intent creation endpoint for storing subscription payment methods.
/// </summary>
public static class CreateSetupIntentEndpoint
{
    /// <summary>
    /// Adds the endpoint that creates a Stripe customer and setup intent for a subscription session.
    /// </summary>
    /// <param name="builder">The endpoint route builder used to register the route.</param>
    /// <returns>The endpoint route builder.</returns>
    public static IEndpointRouteBuilder AddStripeCreateSetupIntentEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("subscriptions/stripe/create-setup-intent", HandleAsync)
            .AllowAnonymous()
            .WithName(SubscriptionConstants.RouteName.CreateSetupIntentEndpoint)
            .DisableAntiforgery()
            .WithMetadata(new RateLimitGroupAttribute(SubscriptionConstants.RateLimitGroups.Payment));

        return builder;
    }

    /// <summary>
    /// Handles a request to create a Stripe customer and setup intent for a subscription payment method.
    /// </summary>
    /// <param name="model">The setup intent creation request.</param>
    /// <param name="stripeOptions">The configured Stripe options.</param>
    /// <param name="subscriptionSessionStore">The store used to load and save subscription sessions.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor used to read the current user.</param>
    /// <param name="paymentAttemptLimiter">The payment attempt limiter used to throttle repeated requests.</param>
    /// <param name="userManager">The user manager used to load authenticated users.</param>
    /// <param name="displayNameProvider">The display name provider used to populate Stripe customer names.</param>
    /// <param name="documentJsonSerializerOptions">The JSON serializer options used to read saved registration data.</param>
    /// <param name="stripeCustomerService">The Stripe customer service used to create or reuse customers.</param>
    /// <param name="stripeSetupIntentService">The Stripe setup intent service used to create setup intents.</param>
    /// <returns>An HTTP result that contains setup intent details or an error response.</returns>
    private static async Task<IResult> HandleAsync(
        [FromBody] CreateSetupIntentPayment model,
        IOptions<StripeOptions> stripeOptions,
        ISubscriptionSessionStore subscriptionSessionStore,
        IHttpContextAccessor httpContextAccessor,
        IPaymentAttemptLimiter paymentAttemptLimiter,
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

        if (!await PaymentEndpointThrottle.AllowAsync(paymentAttemptLimiter, httpContextAccessor.HttpContext, "setup-intent", model.SessionId))
        {
            return PaymentEndpointThrottle.TooManyRequests();
        }

        var session = await subscriptionSessionStore.GetAsync(model.SessionId, SubscriptionSessionStatus.Pending);

        if (session == null)
        {
            return TypedResults.NotFound();
        }

        if (!session.TryGet<Invoice>(out var invoice))
        {
            return TypedResults.NotFound();
        }

        var customerRequest = new CreateCustomerRequest()
        {
            PaymentMethodId = model.PaymentMethodId,
            Metadata = model.Metadata ?? [],
            IdempotencyKey = StripeIdempotencyKey.Compute(
                "sub_cust",
                model.SessionId,
                model.PaymentMethodId),
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
            IdempotencyKey = StripeIdempotencyKey.Compute(
                "sub_si",
                model.SessionId,
                customerResult.CustomerId,
                model.PaymentMethodId),
        };
        intentRequest.Metadata["sessionId"] = model.SessionId;

        var result = await stripeSetupIntentService.CreateAsync(intentRequest);

        session.Put(new StripeMetadata
        {
            CustomerId = customerResult.CustomerId,
            PaymentMethodId = model.PaymentMethodId,
            SetupIntentId = result.Id,
        });

        await subscriptionSessionStore.SaveAsync(session);

        return TypedResults.Ok(new
        {
            id = result.Id,
            clientSecret = result.ClientSecret,
            customerId = customerResult.CustomerId,
            status = result.Status,
            processInitialPayment = (invoice.InitialPaymentAmount ?? 0) > GetMinimumAllowed(invoice.Currency),
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

    private static decimal GetMinimumAllowed(string currency)
    {
        if (StripeLimits.TryGetStripePaymentLimit(currency, out var limits))
        {
            return limits?.Minimum ?? 0;
        }

        return 0;
    }
}
