using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Subscriptions.Endpoints;

/// <summary>
/// The JSON endpoints the down payment page of an installment plan calls: one starts the payment with the card the
/// administrator entered, the other reports whether it settled and, once it has, starts the plan.
/// </summary>
internal static class InstallmentPlanPaymentEndpoints
{
    public static IEndpointRouteBuilder AddInstallmentPlanPaymentEndpoints(this IEndpointRouteBuilder builder)
    {
        var adminUrlPrefix = builder.ServiceProvider.GetRequiredService<IOptions<AdminOptions>>().Value.AdminUrlPrefix;

        builder.MapPost($"{adminUrlPrefix}/installment-plans/BeginPayment/{{itemId}}", BeginPaymentAsync)
            .RequireAuthorization()
            .WithName(SubscriptionConstants.InstallmentPlans.BeginPaymentRouteName);

        builder.MapPost($"{adminUrlPrefix}/installment-plans/PaymentStatus/{{itemId}}", PaymentStatusAsync)
            .RequireAuthorization()
            .WithName(SubscriptionConstants.InstallmentPlans.PaymentStatusRouteName);

        return builder;
    }

    internal static async Task<IResult> BeginPaymentAsync(
        string itemId,
        BeginDownPaymentRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IInstallmentPlanStore planStore,
        ICheckoutEngine checkoutEngine,
        ICheckoutPaymentProviderResolver providerResolver,
        IEnumerable<ICheckoutSavedPaymentMethodProvider> savedPaymentMethodProviders,
        IStringLocalizer<InstallmentPlansController> S)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Results.Forbid();
        }

        if (!await IsValidRequestAsync(antiforgery, httpContext))
        {
            return Results.BadRequest();
        }

        var plan = string.IsNullOrEmpty(itemId) ? null : await planStore.FindByIdAsync(itemId, httpContext.RequestAborted);

        if (plan is null || plan.Status != InstallmentPlanStatus.Draft || string.IsNullOrEmpty(plan.DownPaymentSessionId) || !string.IsNullOrEmpty(plan.SourceTransactionId))
        {
            return Error(S["This plan is not waiting for a down payment."]);
        }

        var provider = InstallmentPlanPaymentMethods.GetEligible(providerResolver, savedPaymentMethodProviders).FirstOrDefault(candidate =>
            string.Equals(candidate.Key, request?.ProviderKey, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            return Error(S["Choose a payment method that can keep the card for later payments."]);
        }

        var outcome = await checkoutEngine.BeginPaymentAsync(
            plan.DownPaymentSessionId,
            new BeginPaymentOptions
            {
                ProviderKey = provider.Key,
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl,
                ProviderData = InstallmentPlanPaymentMethods.BuildProviderData(request.ProviderData, plan.CollectionMethod == InstallmentCollectionMethod.AutoCharge),
            },
            httpContext.RequestAborted);

        if (!outcome.Succeeded)
        {
            return Error(outcome.ProviderErrorMessage ?? outcome.ErrorMessage);
        }

        return Results.Ok(new
        {
            requiresAction = outcome.RequiresAction,
            steps = outcome.Steps.Select(step => new
            {
                obligationId = step.ObligationId,
                attemptId = step.AttemptId,
                clientSecret = step.ClientSecret,
                redirectUrl = step.RedirectUrl,
                requiresAction = step.RequiresAction,
            }),
        });
    }

    internal static async Task<IResult> PaymentStatusAsync(
        string itemId,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IInstallmentPlanStore planStore,
        IInstallmentPlanService planService,
        ICheckoutEngine checkoutEngine)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SubscriptionPermissions.ManageInstallmentPlans))
        {
            return Results.Forbid();
        }

        if (!await IsValidRequestAsync(antiforgery, httpContext))
        {
            return Results.BadRequest();
        }

        var plan = string.IsNullOrEmpty(itemId) ? null : await planStore.FindByIdAsync(itemId, httpContext.RequestAborted);

        if (plan is null || string.IsNullOrEmpty(plan.DownPaymentSessionId))
        {
            return Results.NotFound();
        }

        var result = await checkoutEngine.TryCompleteAsync(plan.DownPaymentSessionId, httpContext.RequestAborted);

        if (result.IsCompleted)
        {
            // The payment handler usually starts the plan as the payment is recorded; this makes sure of it before
            // the administrator is shown the plan.
            await planService.ActivateAsync(plan.ItemId, httpContext.RequestAborted);
        }

        return Results.Ok(new
        {
            status = result.Status.ToString(),
            completed = result.IsCompleted,
            blockingStepKey = result.BlockingStepKey,
            errorMessage = result.ErrorMessage,
        });
    }

    // The page sends its antiforgery token in the RequestVerificationToken header, as a form would in its body.
    private static async Task<bool> IsValidRequestAsync(IAntiforgery antiforgery, HttpContext httpContext)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(httpContext);

            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static IResult Error(string message)
        => Results.Json(new { error = message, errorMessage = message }, statusCode: StatusCodes.Status400BadRequest);
}
